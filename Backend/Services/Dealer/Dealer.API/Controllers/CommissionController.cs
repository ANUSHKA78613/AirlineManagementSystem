using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dealer.Infrastructure.Persistence;
using Dealer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dealer.API.Controllers
{
    [Authorize(Roles = "Dealer,Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class CommissionController : ControllerBase
    {
        private readonly DealerDbContext _context;
        private readonly ILogger<CommissionController> _logger;
        private const decimal DefaultCommissionRate = 0.05m;

        public CommissionController(DealerDbContext context, ILogger<CommissionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> RecordCommission([FromBody] CommissionRequest request)
        {
            if (request.BookingAmount <= 0)
                return BadRequest(new { error = "Booking amount must be positive." });

            var rate = request.CommissionRate > 0 ? request.CommissionRate : DefaultCommissionRate;
            var commissionAmount = request.BookingAmount * rate;

            var commission = new CommissionRecord
            {
                AgentId = request.AgentId,
                AgentCode = request.AgentCode,
                BookingAmount = request.BookingAmount,
                CommissionAmount = commissionAmount,
                PNR = request.PNR,
                Status = "Pending"
            };

            _context.Commissions.Add(commission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Commission recorded: AgentCode={AgentCode}, PNR={PNR}, Amount={Amount}",
                request.AgentCode, request.PNR, commissionAmount);

            return Ok(new { message = $"Commission of {commissionAmount:C} ({rate * 100}%) recorded.", commission });
        }

        /// <summary>
        /// Record commission for a dealer's own bulk booking (wallet-debited, no Razorpay payment event).
        /// Calculates commission using the agent's configured rate.
        /// </summary>
        [Authorize(Roles = "Dealer,Admin")]
        [HttpPost("self")]
        public async Task<IActionResult> RecordSelfCommission([FromBody] CommissionRequest request)
        {
            if (request.BookingAmount <= 0)
                return BadRequest(new { error = "Booking amount must be positive." });

            if (string.IsNullOrWhiteSpace(request.AgentCode))
                return BadRequest(new { error = "AgentCode is required." });

            // Prevent duplicate commission records for the same PNR
            if (!string.IsNullOrWhiteSpace(request.PNR))
            {
                var existing = await _context.Commissions
                    .AnyAsync(c => c.PNR == request.PNR && c.AgentCode == request.AgentCode && !c.IsReversed);
                if (existing)
                    return Conflict(new { error = "Commission already recorded for this PNR." });
            }

            // Look up the agent's actual commission rate from DB
            var agent = await _context.DealerAgents
                .FirstOrDefaultAsync(a => a.AgentCode == request.AgentCode && a.IsActive);
            var rate = (agent?.CommissionRate ?? 0) > 0
                ? (agent!.CommissionRate / 100m)
                : (request.CommissionRate > 0 ? request.CommissionRate : DefaultCommissionRate);

            var commissionAmount = request.BookingAmount * rate;

            var commission = new CommissionRecord
            {
                AgentId = request.AgentId > 0 ? request.AgentId : (agent?.AgentId ?? 0),
                AgentCode = request.AgentCode,
                BookingAmount = request.BookingAmount,
                CommissionAmount = commissionAmount,
                PNR = request.PNR,
                Status = "Paid"   // Dealer bulk bookings are immediately "paid" (deducted from wallet)
            };

            _context.Commissions.Add(commission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Self-commission recorded: AgentCode={AgentCode}, PNR={PNR}, Amount={Amount} ({Rate}%)",
                request.AgentCode, request.PNR, commissionAmount, rate * 100);

            return Ok(new { message = $"Commission of {commissionAmount:C} recorded.", commission });
        }

        [HttpGet("agent/{agentCode}")]
        public async Task<IActionResult> GetByAgent(string agentCode, [FromQuery] string? status = null)
        {
            var query = _context.Commissions.Where(c => c.AgentCode == agentCode);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(c => c.Status == status);

            var commissions = await query.OrderByDescending(c => c.EarnedDate).ToListAsync();
            return Ok(commissions);
        }

        [HttpGet("agent/{agentCode}/summary")]
        public async Task<IActionResult> GetAgentSummary(string agentCode)
        {
            var commissions = await _context.Commissions
                .Where(c => c.AgentCode == agentCode).ToListAsync();

            if (!commissions.Any())
                return NotFound(new { error = "No commissions found for this agent." });

            return Ok(new
            {
                AgentCode = agentCode,
                TotalBookings = commissions.Count,
                TotalBookingAmount = commissions.Sum(c => c.BookingAmount),
                TotalCommissionEarned = commissions.Where(c => !c.IsReversed).Sum(c => c.CommissionAmount),
                PendingCommission = commissions.Where(c => c.Status == "Pending" && !c.IsReversed).Sum(c => c.CommissionAmount),
                PaidCommission = commissions.Where(c => c.Status == "Paid" && !c.IsReversed).Sum(c => c.CommissionAmount),
                ReversedCommission = commissions.Where(c => c.IsReversed).Sum(c => c.CommissionAmount),
                PendingCount = commissions.Count(c => c.Status == "Pending" && !c.IsReversed),
                PaidCount = commissions.Count(c => c.Status == "Paid" && !c.IsReversed),
                ReversedCount = commissions.Count(c => c.IsReversed)
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/pay")]
        public async Task<IActionResult> PayCommission(int id)
        {
            var commission = await _context.Commissions.FindAsync(id);
            if (commission == null) return NotFound(new { error = "Commission record not found." });
            if (commission.IsReversed) return BadRequest(new { error = "Cannot pay a reversed commission." });
            if (commission.Status == "Paid") return BadRequest(new { error = "Commission has already been paid." });

            commission.Status = "Paid";
            commission.PaidDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Commission paid.", commission });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("agent/{agentCode}/pay-all")]
        public async Task<IActionResult> PayAllPending(string agentCode)
        {
            var pending = await _context.Commissions
                .Where(c => c.AgentCode == agentCode && c.Status == "Pending" && !c.IsReversed)
                .ToListAsync();

            if (!pending.Any())
                return NotFound(new { error = "No pending commissions for this agent." });

            foreach (var c in pending) { c.Status = "Paid"; c.PaidDate = DateTime.UtcNow; }
            await _context.SaveChangesAsync();

            var totalPaid = pending.Sum(c => c.CommissionAmount);
            return Ok(new { message = $"Paid {pending.Count} pending commissions totalling {totalPaid:C}.", count = pending.Count, totalPaid });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/reverse")]
        public async Task<IActionResult> ReverseCommission(int id)
        {
            var commission = await _context.Commissions.FindAsync(id);
            if (commission == null) return NotFound(new { error = "Commission record not found." });
            if (commission.IsReversed) return BadRequest(new { error = "Commission has already been reversed." });

            commission.IsReversed = true;
            commission.ReversedAt = DateTime.UtcNow;
            commission.Status = "Reversed";

            var reversal = new CommissionRecord
            {
                AgentId = commission.AgentId,
                AgentCode = commission.AgentCode,
                BookingAmount = -commission.BookingAmount,
                CommissionAmount = -commission.CommissionAmount,
                PNR = commission.PNR,
                Status = "Reversed",
                IsReversed = true,
                ReversedAt = DateTime.UtcNow
            };

            _context.Commissions.Add(reversal);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Commission reversed successfully.", originalCommission = commission, reversalEntry = reversal });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("report")]
        public async Task<IActionResult> GetReport(
            [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? agentCode = null)
        {
            var query = _context.Commissions.AsQueryable();
            if (from.HasValue) query = query.Where(c => c.EarnedDate >= from.Value);
            if (to.HasValue) query = query.Where(c => c.EarnedDate <= to.Value);
            if (!string.IsNullOrWhiteSpace(agentCode)) query = query.Where(c => c.AgentCode == agentCode);

            var commissions = await query.OrderByDescending(c => c.EarnedDate).ToListAsync();
            return Ok(new
            {
                from, to, agentCode,
                totalRecords = commissions.Count,
                totalBookingAmount = commissions.Where(c => !c.IsReversed).Sum(c => c.BookingAmount),
                totalCommissionAmount = commissions.Where(c => !c.IsReversed).Sum(c => c.CommissionAmount),
                commissions
            });
        }
    }

    public class CommissionRequest
    {
        public int AgentId { get; set; }
        public string AgentCode { get; set; } = string.Empty;
        public decimal BookingAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public string? PNR { get; set; }
    }
}
