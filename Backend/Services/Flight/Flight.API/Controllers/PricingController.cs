using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Flight.Infrastructure.Persistence;
using Flight.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flight.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PricingController : ControllerBase
    {
        private readonly FlightDbContext _context;
        private readonly ILogger<PricingController> _logger;

        public PricingController(FlightDbContext context, ILogger<PricingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ─── Flight Pricing (replaces PricingRules) ───────────────────

        [Authorize(Roles = "Admin")]
        [HttpGet("rules")]
        public async Task<IActionResult> GetRules([FromQuery] int? flightId = null)
        {
            var query = _context.FlightPricings.AsQueryable();
            if (flightId.HasValue)
                query = query.Where(r => r.FlightId == flightId.Value);
            return Ok(await query.Where(r => r.IsActive).ToListAsync());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("rules")]
        public async Task<IActionResult> AddRule([FromBody] FlightPricing rule)
        {
            if (rule.BasePrice <= 0)
                return BadRequest(new { error = "BasePrice must be greater than zero." });
            if (rule.Multiplier <= 0)
                return BadRequest(new { error = "Multiplier must be positive." });

            _context.FlightPricings.Add(rule);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Pricing rule added.", rule });
        }

        [AllowAnonymous]
        [HttpGet("calculate")]
        public async Task<IActionResult> Calculate([FromQuery] int flightId, [FromQuery] string seatClass = "Economy")
        {
            var rule = await _context.FlightPricings.FirstOrDefaultAsync(r =>
                r.FlightId == flightId && r.Class == seatClass && r.IsActive);

            if (rule == null)
                return NotFound(new { error = "No pricing rule found for this flight/class." });

            var finalPrice = rule.BasePrice * rule.Multiplier;
            return Ok(new { flightId, seatClass, basePrice = rule.BasePrice, multiplier = rule.Multiplier, finalPrice });
        }

        [AllowAnonymous]
        [HttpGet("flight/{flightId:int}")]
        public async Task<IActionResult> GetFlightPricing(int flightId)
        {
            var pricings = await _context.FlightPricings
                .Where(p => p.FlightId == flightId && p.IsActive)
                .ToListAsync();

            if (!pricings.Any())
                return NotFound(new { error = "No pricing configured for this flight." });

            var result = pricings.Select(p => new
            {
                p.Id,
                p.FlightId,
                seatClass = p.Class,
                p.BasePrice,
                p.Multiplier,
                finalPrice = p.BasePrice * p.Multiplier
            });

            var minPrice = pricings.Min(p => p.BasePrice * p.Multiplier);

            return Ok(new { pricings = result, startingFrom = minPrice });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("rules/{ruleId:int}")]
        public async Task<IActionResult> UpdateRule(int ruleId, [FromBody] FlightPricing updatedRule)
        {
            var rule = await _context.FlightPricings.FindAsync(ruleId);
            if (rule == null) return NotFound(new { error = "Pricing rule not found." });

            if (updatedRule.BasePrice <= 0) return BadRequest(new { error = "BasePrice must be greater than zero." });
            if (updatedRule.Multiplier <= 0) return BadRequest(new { error = "Multiplier must be positive." });

            rule.FlightId = updatedRule.FlightId;
            rule.Class = updatedRule.Class;
            rule.BasePrice = updatedRule.BasePrice;
            rule.Multiplier = updatedRule.Multiplier;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Pricing rule updated.", rule });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("rules/{ruleId:int}")]
        public async Task<IActionResult> DeleteRule(int ruleId)
        {
            var rule = await _context.FlightPricings.FindAsync(ruleId);
            if (rule == null) return NotFound(new { error = "Pricing rule not found." });

            _context.FlightPricings.Remove(rule);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Pricing rule deleted.", ruleId });
        }

        // ─── Seat Configurations ──────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpGet("seat-configs")]
        public async Task<IActionResult> GetSeatConfigs([FromQuery] int? flightId = null)
        {
            var query = _context.SeatConfigurations.AsQueryable();
            if (flightId.HasValue)
                query = query.Where(c => c.FlightId == flightId.Value);
            return Ok(await query.ToListAsync());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("seat-configs")]
        public async Task<IActionResult> AddSeatConfig([FromBody] SeatConfiguration config)
        {
            if (config.TotalCapacity <= 0 || config.TotalRows <= 0)
                return BadRequest(new { error = "Capacity and rows must be positive." });

            _context.SeatConfigurations.Add(config);
            await _context.SaveChangesAsync();

            // Clear existing seats for this flight+class so they regenerate with new layout
            var oldSeats = await _context.Seats
                .Where(s => s.FlightId == config.FlightId && s.SeatClass == config.Class)
                .ToListAsync();
            if (oldSeats.Any())
            {
                _context.Seats.RemoveRange(oldSeats);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Seat configuration added.", config });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("seat-configs/{configId:int}")]
        public async Task<IActionResult> UpdateSeatConfig(int configId, [FromBody] SeatConfiguration updated)
        {
            var config = await _context.SeatConfigurations.FindAsync(configId);
            if (config == null) return NotFound(new { error = "Configuration not found." });

            config.TotalCapacity = updated.TotalCapacity;
            config.TotalRows = updated.TotalRows;
            config.ColumnsLayout = updated.ColumnsLayout;
            config.AisleMarkup = updated.AisleMarkup;
            config.WindowMarkup = updated.WindowMarkup;
            config.MiddleMarkup = updated.MiddleMarkup;

            // Clear existing seats for this flight+class so they regenerate
            var oldSeats = await _context.Seats
                .Where(s => s.FlightId == config.FlightId && s.SeatClass == config.Class)
                .ToListAsync();
            if (oldSeats.Any())
            {
                _context.Seats.RemoveRange(oldSeats);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Seat configuration updated.", config });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("seat-configs/{configId:int}")]
        public async Task<IActionResult> DeleteSeatConfig(int configId)
        {
            var config = await _context.SeatConfigurations.FindAsync(configId);
            if (config == null) return NotFound(new { error = "Configuration not found." });

            _context.SeatConfigurations.Remove(config);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Seat configuration deleted.", configId });
        }

        // ─── Coupons (DB-backed) ─────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpGet("coupons")]
        public async Task<IActionResult> GetCoupons()
        {
            var coupons = await _context.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync();
            return Ok(coupons);
        }

        [AllowAnonymous]
        [HttpGet("active-coupons")]
        public async Task<IActionResult> GetActiveCoupons()
        {
            var coupons = await _context.Coupons
                .Where(c => c.IsActive && c.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return Ok(coupons);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("coupons")]
        public async Task<IActionResult> AddCoupon([FromBody] Coupon coupon)
        {
            if (string.IsNullOrWhiteSpace(coupon.Code))
                return BadRequest(new { error = "Coupon code is required." });

            var exists = await _context.Coupons.AnyAsync(c => c.Code == coupon.Code);
            if (exists) return Conflict(new { error = $"Coupon code '{coupon.Code}' already exists." });

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Coupon created: {Code}, Discount: {Discount}%", coupon.Code, coupon.DiscountPercent);
            return Ok(new { message = "Coupon created.", coupon });
        }

        [HttpPost("coupons/validate")]
        public async Task<IActionResult> ValidateCoupon([FromBody] CouponValidationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { error = "Coupon code is required." });

            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == request.Code);
            if (coupon == null)
                return NotFound(new { valid = false, error = "Coupon not found." });

            if (!coupon.IsActive)
                return Ok(new { valid = false, error = "Coupon is inactive." });
            if (coupon.ExpiresAt < DateTime.UtcNow)
                return Ok(new { valid = false, error = "Coupon has expired." });

            // Enforce logged in user only so we can track usage per user
            if (request.UserId <= 0)
            {
                return Ok(new { valid = false, error = "Coupons are only available for logged-in users." });
            }

            var userUsageCount = await _context.CouponUsages
                .CountAsync(cu => cu.CouponCode == request.Code && cu.UserId == request.UserId);

            if (userUsageCount >= coupon.UsageLimit)
                return Ok(new { valid = false, error = $"You have already used this coupon {userUsageCount} time(s). Maximum allowed: {coupon.UsageLimit}." });

            var discountAmount = request.Amount * (coupon.DiscountPercent / 100m);
            var finalAmount = request.Amount - discountAmount;

            return Ok(new
            {
                valid = true,
                code = coupon.Code,
                discountPercent = coupon.DiscountPercent,
                originalAmount = request.Amount,
                discountAmount,
                finalAmount
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("coupons/{couponId:int}")]
        public async Task<IActionResult> DeleteCoupon(int couponId)
        {
            var coupon = await _context.Coupons.FindAsync(couponId);
            if (coupon == null) return NotFound(new { error = "Coupon not found." });

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Coupon deleted.", couponId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("coupons/{couponId:int}")]
        public async Task<IActionResult> UpdateCoupon(int couponId, [FromBody] Coupon updatedCoupon)
        {
            var coupon = await _context.Coupons.FindAsync(couponId);
            if (coupon == null) return NotFound(new { error = "Coupon not found." });

            if (string.IsNullOrWhiteSpace(updatedCoupon.Code))
                return BadRequest(new { error = "Coupon code is required." });

            var exists = await _context.Coupons.AnyAsync(c => c.Code == updatedCoupon.Code && c.CouponId != couponId);
            if (exists) return Conflict(new { error = $"Coupon code '{updatedCoupon.Code}' already exists." });

            coupon.Code = updatedCoupon.Code;
            coupon.DiscountPercent = updatedCoupon.DiscountPercent;
            coupon.UsageLimit = updatedCoupon.UsageLimit;
            coupon.ExpiresAt = updatedCoupon.ExpiresAt;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Coupon updated.", coupon });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("coupons/{couponId:int}/toggle")]
        public async Task<IActionResult> ToggleCoupon(int couponId)
        {
            var coupon = await _context.Coupons.FindAsync(couponId);
            if (coupon == null) return NotFound(new { error = "Coupon not found." });

            coupon.IsActive = !coupon.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Coupon status updated.", couponId, isActive = coupon.IsActive });
        }
    }

    public class CouponValidationRequest
    {
        public string Code { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int UserId { get; set; }
    }
}
