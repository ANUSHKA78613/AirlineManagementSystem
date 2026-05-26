using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operations.Application.DTOs;
using Operations.Application.CQRS.Commands;
using Operations.Infrastructure.Persistence;
using Operations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Middleware.Exceptions;

namespace Operations.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OperationsController : ControllerBase
    {
        private static readonly HashSet<string> BoardedPassengers = new(StringComparer.OrdinalIgnoreCase);

        private readonly IMediator _mediator;
        private readonly OperationsDbContext _context;
        public OperationsController(IMediator mediator, OperationsDbContext context) { _mediator = mediator; _context = context; }

        // ─── Employee Endpoints ─────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpPost("employees")]
        public async Task<IActionResult> AddEmployee([FromBody] AddEmployeeDto dto)
        {
            return Ok(new { message = await _mediator.Send(new AddEmployeeCommand(dto)) });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("employees")]
        public async Task<IActionResult> GetAll() => Ok(await _context.Employees.ToListAsync());

        // ─── Check-In Endpoints ────────────────────────────────────────

        [HttpPost("checkin")]
        public async Task<IActionResult> WebCheckIn([FromBody] CheckInDto dto)
        {
            return Ok(await _mediator.Send(new CheckInCommand(dto)));
        }

        [HttpGet("checkin-status/{pnr}")]
        public async Task<IActionResult> GetCheckInStatus(string pnr)
        {
            var checkIns = await _context.CheckIns.AsNoTracking().Where(c => c.PNR == pnr).ToListAsync();
            var passes = await _context.BoardingPasses.AsNoTracking().Where(b => b.PNR == pnr).ToListAsync();
            var result = checkIns.Select(c => new {
                passengerId = c.PassengerId,
                seatNo = c.SeatNo,
                status = c.Status,
                gate = passes.FirstOrDefault(p => p.PassengerId == c.PassengerId)?.Gate,
                boardingTime = passes.FirstOrDefault(p => p.PassengerId == c.PassengerId)?.BoardingTime
            });
            return Ok(result);
        }

        // ─── Boarding Pass Endpoints ───────────────────────────────────

        [HttpGet("boardingpass/{pnr}/{passengerId}")]
        public async Task<IActionResult> GetBoardingPass(string pnr, int passengerId)
        {
            var pass = await _context.BoardingPasses.FirstOrDefaultAsync(b => b.PNR == pnr && b.PassengerId == passengerId);
            if (pass == null) return NotFound(new { error = "Boarding pass not found." });
            return Ok(pass);
        }

        [AllowAnonymous]
        [HttpGet("boardingpass/{pnr}/{passengerId}/download")]
        public async Task<IActionResult> DownloadBoardingPass(string pnr, int passengerId)
        {
            var pass = await _context.BoardingPasses.FirstOrDefaultAsync(b => b.PNR == pnr && b.PassengerId == passengerId);
            if (pass == null) return NotFound(new { error = "Boarding pass not found." });

            var html = $@"<!DOCTYPE html>
<html><head><title>Boarding Pass — {pass.PNR}</title>
<style>
body {{ font-family: 'Segoe UI', sans-serif; background: #f4f6f9; padding: 40px; }}
.card {{ max-width: 600px; margin: auto; background: #fff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,.1); overflow: hidden; }}
.header {{ background: linear-gradient(135deg, #0061ff, #60efff); color: #fff; padding: 20px 30px; font-size: 22px; font-weight: 700; }}
.body {{ padding: 24px 30px; }}
.row {{ display: flex; justify-content: space-between; margin-bottom: 14px; }}
.label {{ color: #888; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }}
.value {{ font-size: 18px; font-weight: 600; }}
.qr {{ text-align: center; padding: 20px; border-top: 2px dashed #ddd; margin-top: 10px; font-family: monospace; font-size: 14px; word-break: break-all; color: #555; }}
</style></head>
<body><div class='card'>
<div class='header'>✈ SkyHorizon Boarding Pass</div>
<div class='body'>
<div class='row'><div><div class='label'>PNR</div><div class='value'>{pass.PNR}</div></div><div><div class='label'>Passenger ID</div><div class='value'>{pass.PassengerId}</div></div></div>
<div class='row'><div><div class='label'>Seat</div><div class='value'>{pass.SeatNo}</div></div><div><div class='label'>Gate</div><div class='value'>{pass.Gate}</div></div></div>
<div class='row'><div><div class='label'>Boarding Time</div><div class='value'>{pass.BoardingTime:HH:mm}</div></div><div><div class='label'>Gate</div><div class='value'>{pass.Gate}</div></div></div>
</div>
<div class='qr'>QR: {pass.QRCode}</div>
</div></body></html>";

            return Content(html, "text/html");
        }

        [AllowAnonymous]
        [HttpGet("boardingpass/{pnr}/download")]
        public async Task<IActionResult> DownloadAllBoardingPasses(string pnr)
        {
            var passes = await _context.BoardingPasses.Where(b => b.PNR == pnr).ToListAsync();
            if (passes == null || passes.Count == 0) return NotFound(new { error = "No boarding passes found for this PNR." });

            var html = $@"<!DOCTYPE html>
<html><head><title>Boarding Passes — {pnr}</title>
<style>
body {{ font-family: 'Segoe UI', sans-serif; background: #f4f6f9; padding: 20px; }}
.card {{ max-width: 600px; margin: 20px auto; background: #fff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,.1); overflow: hidden; }}
.header {{ background: linear-gradient(135deg, #0061ff, #60efff); color: #fff; padding: 20px 30px; font-size: 22px; font-weight: 700; }}
.body {{ padding: 24px 30px; }}
.row {{ display: flex; justify-content: space-between; margin-bottom: 14px; }}
.label {{ color: #888; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; }}
.value {{ font-size: 18px; font-weight: 600; }}
.qr {{ text-align: center; padding: 20px; border-top: 2px dashed #ddd; margin-top: 10px; font-family: monospace; font-size: 14px; word-break: break-all; color: #555; }}
@media print {{
  body {{ background: #fff; padding: 0; }}
  .card {{ box-shadow: none; border: 1px solid #ddd; page-break-after: always; margin: 0 auto; }}
  .card:last-child {{ page-break-after: auto; }}
}}
</style></head>
<body><script>window.onload = function() {{ window.print(); }}</script>";

            foreach (var pass in passes)
            {
                html += $@"
<div class='card'>
<div class='header'>✈ SkyHorizon Boarding Pass</div>
<div class='body'>
<div class='row'><div><div class='label'>PNR</div><div class='value'>{pass.PNR}</div></div><div><div class='label'>Passenger ID</div><div class='value'>{pass.PassengerId}</div></div></div>
<div class='row'><div><div class='label'>Seat</div><div class='value'>{pass.SeatNo}</div></div><div><div class='label'>Gate</div><div class='value'>{pass.Gate}</div></div></div>
<div class='row'><div><div class='label'>Boarding Time</div><div class='value'>{pass.BoardingTime:HH:mm}</div></div></div>
</div>
<div class='qr'>QR: {pass.QRCode}</div>
</div>";
            }
            html += "</body></html>";

            return Content(html, "text/html");
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost("boarding/scan")]
        public async Task<IActionResult> ScanBoarding([FromBody] BoardingScanRequest request)
        {
            var pass = await _context.BoardingPasses.FirstOrDefaultAsync(b => b.PNR == request.PNR && b.PassengerId == request.PassengerId);
            if (pass == null) return NotFound(new { error = "Boarding pass not found." });
            if (!string.Equals(pass.QRCode, request.QrCode, StringComparison.Ordinal))
                return BadRequest(new { error = "Invalid QR code." });

            var boardingKey = $"{request.PNR}:{request.PassengerId}";
            if (!BoardedPassengers.Add(boardingKey))
                return Conflict(new { error = "Passenger already boarded." });

            return Ok(new
            {
                message = "Passenger boarded successfully.",
                pnr = request.PNR,
                passengerId = request.PassengerId,
                scannedAt = DateTime.UtcNow
            });
        }

        // ─── Identity Verification ─────────────────────────────────────

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost("verify-identity")]
        public async Task<IActionResult> VerifyIdentity([FromBody] VerifyIdentityRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PNR))
                return BadRequest(new { error = "PNR is required." });
            if (request.PassengerId <= 0)
                return BadRequest(new { error = "Valid passenger ID is required." });

            var pass = await _context.BoardingPasses.FirstOrDefaultAsync(
                b => b.PNR == request.PNR && b.PassengerId == request.PassengerId);

            if (pass == null)
                return NotFound(new { error = "No boarding pass found for this PNR and passenger." });

            return Ok(new
            {
                verified = true,
                pnr = request.PNR,
                passengerId = request.PassengerId,
                seatNo = pass.SeatNo,
                gate = pass.Gate,
                message = "Identity verified successfully."
            });
        }

        // ─── Baggage Endpoints (DB-backed) ─────────────────────────────

        [HttpGet("baggage")]
        public async Task<IActionResult> GetBaggage([FromQuery] string? pnr = null)
        {
            var query = _context.BaggageRecords.AsQueryable();
            if (!string.IsNullOrWhiteSpace(pnr))
                query = query.Where(b => b.PNR == pnr);

            return Ok(await query.OrderByDescending(b => b.CreatedAt).ToListAsync());
        }

        [HttpPost("baggage")]
        public async Task<IActionResult> AddBaggage([FromBody] Baggage baggage)
        {
            if (string.IsNullOrWhiteSpace(baggage.PNR))
                return BadRequest(new { error = "PNR is required." });
            if (baggage.PassengerId <= 0)
                return BadRequest(new { error = "Passenger ID is required." });
            if (baggage.Weight <= 0)
                return BadRequest(new { error = "Baggage weight must be greater than zero." });
            if (string.IsNullOrWhiteSpace(baggage.TagNumber))
                return BadRequest(new { error = "Baggage tag number is required." });

            var exists = await _context.BaggageRecords.AnyAsync(b => b.TagNumber == baggage.TagNumber);
            if (exists)
                return Conflict(new { error = "Baggage tag already exists." });

            if (string.IsNullOrWhiteSpace(baggage.Status))
                baggage.Status = baggage.Weight > 25 ? "Overweight" : "Checked";

            baggage.CreatedAt = DateTime.UtcNow;
            _context.BaggageRecords.Add(baggage);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Baggage record added.", baggage });
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("baggage/{id}/status")]
        public async Task<IActionResult> UpdateBaggageStatus(int id, [FromBody] UpdateBaggageStatusRequest request)
        {
            var validStatuses = new[] { "Checked", "Overweight", "Lost", "Found", "Delivered" };
            if (!validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { error = $"Invalid status. Allowed: {string.Join(", ", validStatuses)}" });

            var baggage = await _context.BaggageRecords.FindAsync(id);
            if (baggage == null) return NotFound(new { error = "Baggage record not found." });

            baggage.Status = request.Status;
            baggage.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Baggage status updated to '{request.Status}'.", baggage });
        }

        // ─── Issue Endpoints (DB-backed) ───────────────────────────────

        [Authorize]
        [HttpGet("issues/passenger/{email}")]
        public async Task<IActionResult> GetPassengerIssues(string email)
        {
            var userEmailClaim = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (userEmailClaim == null || !string.Equals(userEmailClaim, email, StringComparison.OrdinalIgnoreCase))
            {
                var isStaffOrAdmin = User.IsInRole("Staff") || User.IsInRole("Admin");
                if (!isStaffOrAdmin) return Forbid();
            }

            var issues = await _context.Issues
                .AsNoTracking()
                .Where(i => i.PassengerEmail == email)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return Ok(issues);
        }

        [Authorize]
        [HttpPost("issues/passenger")]
        public async Task<IActionResult> AddPassengerIssue([FromBody] AddPassengerIssueRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { error = "Issue title is required." });
            
            var userEmail = User.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(userEmail))
                return Unauthorized(new { error = "User email not found in token." });

            var issue = new Issue
            {
                Title = request.Title,
                Description = request.Description,
                PassengerEmail = userEmail,
                Category = "Customer Care",
                CreatedAt = DateTime.UtcNow,
                Status = "Open",
                Priority = "Medium"
            };

            _context.Issues.Add(issue);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Issue submitted successfully.", issue });
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpGet("issues")]
        public async Task<IActionResult> GetIssues([FromQuery] string? status = null)
        {
            var query = _context.Issues.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(i => i.Status == status);

            return Ok(await query.OrderByDescending(i => i.CreatedAt).ToListAsync());
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPost("issues")]
        public async Task<IActionResult> AddIssue([FromBody] Issue issue)
        {
            if (string.IsNullOrWhiteSpace(issue.Category))
                return BadRequest(new { error = "Issue category is required." });
            if (string.IsNullOrWhiteSpace(issue.Title))
                return BadRequest(new { error = "Issue title is required." });

            var duplicate = await _context.Issues.AnyAsync(i =>
                i.Category == issue.Category &&
                i.Title == issue.Title &&
                i.CreatedAt >= DateTime.UtcNow.AddHours(-6));
            if (duplicate)
                return Conflict(new { error = "A similar issue was already logged recently." });

            issue.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(issue.Status)) issue.Status = "Open";
            _context.Issues.Add(issue);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Issue logged.", issue });
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("issues/{id}/resolve")]
        public async Task<IActionResult> ResolveIssue(int id)
        {
            var issue = await _context.Issues.FindAsync(id);
            if (issue == null) return NotFound(new { error = "Issue not found." });
            if (issue.Status == "Resolved" || issue.Status == "Closed")
                return BadRequest(new { error = "Issue is already resolved/closed." });

            issue.Status = "Resolved";
            issue.ResolvedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Issue resolved.", issue });
        }

        [Authorize(Roles = "Staff,Admin")]
        [HttpPut("issues/{id}/reply")]
        public async Task<IActionResult> ReplyIssue(int id, [FromBody] StaffReplyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Reply))
                return BadRequest(new { error = "Reply message is required." });

            var issue = await _context.Issues.FindAsync(id);
            if (issue == null) return NotFound(new { error = "Issue not found." });

            issue.StaffReply = request.Reply;
            issue.Status = "Resolved";
            issue.ResolvedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(issue.PassengerEmail))
            {
                var notification = new NotificationRecord
                {
                    Email = issue.PassengerEmail,
                    Subject = $"Customer Support Update: {issue.Title}",
                    Message = $"Staff replied: {request.Reply}",
                    Status = "Sent",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Reply sent and issue resolved.", issue });
        }

        // ─── Request DTOs ──────────────────────────────────────────────

        public class BoardingScanRequest
        {
            public string PNR { get; set; } = string.Empty;
            public int PassengerId { get; set; }
            public string QrCode { get; set; } = string.Empty;
        }

        public class VerifyIdentityRequest
        {
            public string PNR { get; set; } = string.Empty;
            public int PassengerId { get; set; }
        }

        public class UpdateBaggageStatusRequest
        {
            public string Status { get; set; } = string.Empty;
        }

        public class StaffReplyRequest
        {
            public string Reply { get; set; } = string.Empty;
        }

        public class AddPassengerIssueRequest
        {
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
        }
    }
}
