using Microsoft.Extensions.Configuration;
using Serilog;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.IO;
using Shared.Middleware.Exceptions;

namespace Shared.EmailService
{
    /// <summary>
    /// SMTP-based email service using MailKit.
    /// Reads config from appsettings: SmtpSettings section.
    /// </summary>
    public class SmtpEmailService : IEmailService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly bool _useSsl;

        public SmtpEmailService(IConfiguration configuration)
        {
            var section = configuration.GetSection("SmtpSettings");
            _host = section["Host"] ?? "smtp.gmail.com";
            _port = int.TryParse(section["Port"], out var p) ? p : 587;
            _username = section["Username"] ?? "";
            _password = section["Password"] ?? "";
            _fromEmail = section["FromEmail"] ?? _username;
            _fromName = section["FromName"] ?? "SkyHorizon Airlines";
            _useSsl = bool.TryParse(section["UseSsl"], out var ssl) && ssl;

            Log.Information("SMTP Config loaded: Host={Host}, Port={Port}, Username={Username}, PasswordLength={PwdLen}, FromEmail={From}, UseSsl={Ssl}",
                _host, _port, _username, _password?.Length ?? 0, _fromEmail, _useSsl);
        }

        public async Task SendOtpEmailAsync(string toEmail, string userName, string otpCode, int expiryMinutes = 5)
        {
            var subject = $"🔐 Your SkyHorizon Verification Code: {otpCode}";
            var html = EmailTemplates.OtpTemplate(userName, otpCode, expiryMinutes);
            // We want to throw for OTP to ensure business logic bubbles up the error if it fails
            await SendEmailInternalAsync(toEmail, subject, html, null, null, throwOnError: true);
        }

        public async Task SendBookingConfirmationAsync(string toEmail, string userName, BookingEmailModel booking)
        {
            var subject = $"✈️ Booking Confirmed — PNR: {booking.PNR}";
            var html = EmailTemplates.BookingConfirmationTemplate(userName, booking);
            
            // Generate a lightweight HTML Boarding pass wrapper
            var boardingPassHtml = $@"
<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background: #eee; padding: 20px;'>
<div style='max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 4px 10px rgba(0,0,0,0.1); border-top: 10px solid #f2ca50;'>
<h1 style='color: #333; margin-bottom: 20px;'>✈ SkyHorizon E-Ticket &amp; Boarding Pass</h1>
<p><strong>PNR:</strong> {booking.PNR}</p>
<p><strong>Flight:</strong> {booking.FlightNumber}</p>
<p><strong>Route:</strong> {booking.Source} &#10140; {booking.Destination}</p>
<p><strong>Departure:</strong> {booking.DepartureTime:g}</p>
<p><strong>Arrival:</strong> {booking.ArrivalTime:g}</p>
<hr style='border: 0; height: 1px; background: #ccc; margin: 20px 0;'>
<h3>Passengers:</h3>
<ul style='list-style: none; padding: 0;'>
{string.Join("", booking.Passengers.Select(p => $"<li style='padding: 10px; background: #f9f9f9; margin-bottom: 5px; border-radius: 5px;'><strong>{p.Name}</strong> - Seat: {p.SeatNo}</li>"))}
</ul>
<div style='margin-top: 30px; padding: 20px; background: #f0f8ff; border-radius: 5px; text-align: center'>
<p style='margin:0; font-size: 14px; color: #555;'>Please present this document (digital or printed) along with a valid Government ID at the airport check-in counter and boarding gate.</p>
</div>
</div>
</body></html>";

            var boardingPassBytes = System.Text.Encoding.UTF8.GetBytes(boardingPassHtml);
            await SendEmailInternalAsync(toEmail, subject, html, boardingPassBytes, $"BoardingPass_{booking.PNR}.html", throwOnError: false);
        }

        public async Task SendBookingCancellationAsync(string toEmail, string userName, string pnr, string flightNumber, decimal refundAmount)
        {
            var subject = $"❌ Booking Cancelled — PNR: {pnr}";
            var html = EmailTemplates.BookingCancellationTemplate(userName, pnr, flightNumber, refundAmount);
            await SendEmailInternalAsync(toEmail, subject, html, null, null, throwOnError: false);
        }

        public async Task SendBoardingPassAsync(string toEmail, string userName, string pnr, byte[] pdfAttachment)
        {
            var subject = $"🎫 Your Boarding Pass is Ready — PNR: {pnr}";
            var html = $"<h3>Hi {userName},</h3><p>Your boarding pass for PNR <strong>{pnr}</strong> has been generated successfully.</p><p>Please find the PDF attached to this email.</p><br/><p>Safe Travels,<br/>SkyHorizon Airlines</p>";
            await SendEmailInternalAsync(toEmail, subject, html, pdfAttachment, $"BoardingPass_{pnr}.pdf", throwOnError: false);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            await SendEmailInternalAsync(toEmail, subject, htmlBody, null, null, throwOnError: false);
        }

        private async Task SendEmailInternalAsync(string toEmail, string subject, string htmlBody, byte[]? attachmentBytes, string? attachmentName, bool throwOnError)
        {
            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
            {
                var msg = $"SMTP credentials not configured. Email to {toEmail} with subject '{subject}' was NOT sent.";
                Log.Warning(msg);
                if (throwOnError) throw new ServiceUnavailableException("Email");
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_fromName, _fromEmail));
           message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlBody };

                if (attachmentBytes != null && !string.IsNullOrWhiteSpace(attachmentName))
                {
                    builder.Attachments.Add(attachmentName, attachmentBytes, ContentType.Parse("application/octet-stream"));
                }

                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                client.Timeout = 10000; // 10s timeout

                // MailKit SecureSocketOptions.StartTls is best for port 587
                var secureOptions = _port == 465
                    ? SecureSocketOptions.SslOnConnect
                    : (_useSsl || _port == 587 ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

                Log.Information("Connecting to SMTP {Host}:{Port} with Options={Options}", _host, _port, secureOptions);
                await client.ConnectAsync(_host, _port, secureOptions);

                await client.AuthenticateAsync(_username, _password);

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                Log.Information("Email sent successfully to {Email}: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                Log.Error("SMTP FULL ERROR: {Message} | Inner: {Inner} | Stack: {Stack}",
                    ex.Message, ex.InnerException?.Message ?? "none", ex.StackTrace);
                if (throwOnError)
                {
                    throw new ServiceUnavailableException("Email");
                }
            }
        }
    }
}
