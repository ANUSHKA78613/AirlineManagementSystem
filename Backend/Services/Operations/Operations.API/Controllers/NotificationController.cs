using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operations.Infrastructure.Persistence;
using Operations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.EmailService;

namespace Operations.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly OperationsDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(OperationsDbContext context, IEmailService emailService, ILogger<NotificationController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] string? email = null, [FromQuery] string? status = null)
        {
            var query = _context.Notifications.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(n => n.Email == email);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(n => n.Status == status);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null) return NotFound(new { error = "Notification not found." });
            return Ok(notification);
        }

        /// <summary>Send an email notification via SMTP and record in DB.</summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { error = "Email is required." });
            if (string.IsNullOrWhiteSpace(dto.Subject))
                return BadRequest(new { error = "Subject is required." });

            // Record in DB
            var notification = new NotificationRecord
            {
                UserId = dto.UserId,
                Email = dto.Email,
                Subject = dto.Subject,
                Message = dto.Message ?? string.Empty,
                Status = "Sending"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Send real email via SMTP
            try
            {
                var htmlBody = BuildGenericEmailHtml(dto.Subject, dto.Message ?? string.Empty);
                await _emailService.SendEmailAsync(dto.Email, dto.Subject, htmlBody);

                notification.Status = "Sent";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Email sent to {Email}: {Subject}", dto.Email, dto.Subject);
                return Ok(new { message = $"Email sent to {dto.Email}.", notification });
            }
            catch (Exception ex)
            {
                notification.Status = "Failed";
                await _context.SaveChangesAsync();

                _logger.LogError(ex, "Failed to send email to {Email}", dto.Email);
                return Ok(new { message = $"Email recorded but delivery failed: {ex.Message}", notification });
            }
        }

        /// <summary>Send a booking confirmation email.</summary>
        [HttpPost("send-booking-confirmation")]
        public async Task<IActionResult> SendBookingConfirmation([FromBody] BookingNotificationRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { error = "Email is required." });
            if (string.IsNullOrWhiteSpace(dto.PNR))
                return BadRequest(new { error = "PNR is required." });

            var notification = new NotificationRecord
            {
                UserId = dto.UserId,
                Email = dto.Email,
                Subject = $"Booking Confirmed — PNR: {dto.PNR}",
                Message = $"Flight: {dto.FlightNumber}, From: {dto.Source} To: {dto.Destination}",
                Status = "Sending"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            try
            {
                var bookingModel = new BookingEmailModel
                {
                    PNR = dto.PNR,
                    FlightNumber = dto.FlightNumber ?? $"AG-{dto.FlightId:D4}",
                    Source = dto.Source ?? "DEP",
                    Destination = dto.Destination ?? "ARR",
                    DepartureTime = dto.DepartureTime ?? DateTime.UtcNow.AddHours(24),
                    ArrivalTime = dto.ArrivalTime ?? DateTime.UtcNow.AddHours(27),
                    TotalAmount = dto.TotalAmount,
                    Status = "Confirmed",
                    Passengers = dto.Passengers?.Select(p => new PassengerEmailModel
                    {
                        Name = p.Name,
                        Age = p.Age,
                        SeatNo = p.SeatNo ?? "TBD"
                    }).ToList() ?? new()
                };

                await _emailService.SendBookingConfirmationAsync(dto.Email, dto.UserName ?? "Traveller", bookingModel);

                notification.Status = "Sent";
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Booking confirmation email sent to {dto.Email}.", notification });
            }
            catch (Exception ex)
            {
                notification.Status = "Failed";
                await _context.SaveChangesAsync();

                _logger.LogError(ex, "Failed to send booking confirmation to {Email}", dto.Email);
                return Ok(new { message = $"Email recorded but delivery failed.", notification });
            }
        }

        /// <summary>Send an OTP email.</summary>
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtpEmail([FromBody] OtpNotificationRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { error = "Email is required." });
            if (string.IsNullOrWhiteSpace(dto.OtpCode))
                return BadRequest(new { error = "OTP code is required." });

            var notification = new NotificationRecord
            {
                UserId = dto.UserId,
                Email = dto.Email,
                Subject = "OTP Verification",
                Message = $"OTP: {dto.OtpCode}",
                Status = "Sending"
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendOtpEmailAsync(dto.Email, dto.UserName ?? "User", dto.OtpCode, dto.ExpiryMinutes);

                notification.Status = "Sent";
                await _context.SaveChangesAsync();

                return Ok(new { message = $"OTP email sent to {dto.Email}.", notification });
            }
            catch (Exception ex)
            {
                notification.Status = "Failed";
                await _context.SaveChangesAsync();

                return Ok(new { message = $"OTP email recorded but delivery failed.", notification });
            }
        }

        /// <summary>Get notification statistics.</summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var total = await _context.Notifications.CountAsync();
            var sent = await _context.Notifications.CountAsync(n => n.Status == "Sent");
            var failed = await _context.Notifications.CountAsync(n => n.Status == "Failed");
            var pending = await _context.Notifications.CountAsync(n => n.Status == "Sending" || n.Status == "Pending");

            return Ok(new { total, sent, failed, pending });
        }

        // ─── Helpers ────────────────────────────────────────────────────

        private static string BuildGenericEmailHtml(string subject, string message)
        {
            return $@"
<!DOCTYPE html>
<html><head><meta charset=""UTF-8""></head>
<body style=""margin:0;padding:0;background:#f0f4f8;font-family:'Segoe UI',sans-serif;"">
<table role=""presentation"" width=""100%"" style=""padding:40px 20px;"">
<tr><td align=""center"">
<table role=""presentation"" width=""560"" style=""max-width:560px;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,.08);"">
  <tr><td style=""background:linear-gradient(135deg,#0061FF,#60EFFF);padding:28px 40px;text-align:center;"">
    <h1 style=""margin:0;color:#fff;font-size:24px;font-weight:800;"">✈ SkyHorizon</h1>
  </td></tr>
  <tr><td style=""padding:32px 40px;"">
    <h2 style=""margin:0 0 16px;color:#1a1a2e;font-size:20px;"">{subject}</h2>
    <p style=""margin:0;color:#6b7280;font-size:14px;line-height:1.7;white-space:pre-line;"">{message}</p>
  </td></tr>
  <tr><td style=""background:#f8fafc;padding:16px 40px;border-top:1px solid #e5e7eb;text-align:center;"">
    <p style=""margin:0;color:#9ca3af;font-size:12px;"">© {DateTime.UtcNow.Year} SkyHorizon Airlines</p>
  </td></tr>
</table>
</td></tr>
</table>
</body>
</html>";
        }
    }

    // ─── Request Models ─────────────────────────────────────────────

    public class SendNotificationRequest
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    public class BookingNotificationRequest
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string PNR { get; set; } = string.Empty;
        public int FlightId { get; set; }
        public string? FlightNumber { get; set; }
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureTime { get; set; }
        public DateTime? ArrivalTime { get; set; }
        public decimal TotalAmount { get; set; }
        public List<PassengerNotificationDto>? Passengers { get; set; }
    }

    public class PassengerNotificationDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? SeatNo { get; set; }
    }

    public class OtpNotificationRequest
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string OtpCode { get; set; } = string.Empty;
        public int ExpiryMinutes { get; set; } = 5;
    }
}
