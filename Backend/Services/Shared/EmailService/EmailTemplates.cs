namespace Shared.EmailService
{
    /// <summary>
    /// Premium HTML email templates for SkyHorizon Airlines.
    /// All templates are inline-styled for maximum email client compatibility.
    /// </summary>
    public static class EmailTemplates
    {
        private const string PrimaryColor = "#0061FF";
        private const string GradientStart = "#0061FF";
        private const string GradientEnd = "#60EFFF";
        private const string DarkBg = "#1a1a2e";
        private const string CardBg = "#ffffff";
        private const string TextPrimary = "#1a1a2e";
        private const string TextSecondary = "#6b7280";

        // ─── OTP Email Template ─────────────────────────────────────────

        public static string OtpTemplate(string userName, string otpCode, int expiryMinutes)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f0f4f8;font-family:'Segoe UI',Roboto,Arial,sans-serif;"">
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color:#f0f4f8;padding:40px 20px;"">
<tr><td align=""center"">
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""560"" style=""max-width:560px;background:{CardBg};border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">

  <!-- Header -->
  <tr><td style=""background:linear-gradient(135deg,{GradientStart},{GradientEnd});padding:32px 40px;text-align:center;"">
    <h1 style=""margin:0;color:#fff;font-size:28px;font-weight:800;letter-spacing:1px;"">✈ SkyHorizon</h1>
    <p style=""margin:8px 0 0;color:rgba(255,255,255,0.9);font-size:14px;"">Secure Verification</p>
  </td></tr>

  <!-- Body -->
  <tr><td style=""padding:40px;"">
    <h2 style=""margin:0 0 8px;color:{TextPrimary};font-size:22px;"">Hello, {userName}! 👋</h2>
    <p style=""margin:0 0 24px;color:{TextSecondary};font-size:15px;line-height:1.6;"">
      We received a request to verify your identity. Use the code below to complete the process.
    </p>

    <!-- OTP Box -->
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
    <tr><td align=""center"" style=""padding:24px 0;"">
      <div style=""display:inline-block;background:linear-gradient(135deg,{GradientStart}08,{GradientEnd}15);border:2px dashed {PrimaryColor};border-radius:12px;padding:20px 48px;"">
        <span style=""font-size:36px;font-weight:800;letter-spacing:12px;color:{PrimaryColor};font-family:'Courier New',monospace;"">{otpCode}</span>
      </div>
    </td></tr>
    </table>

    <!-- Expiry Warning -->
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin-top:16px;"">
    <tr><td style=""background:#FFF3CD;border-left:4px solid #FFC107;border-radius:8px;padding:14px 18px;"">
      <p style=""margin:0;color:#856404;font-size:13px;"">
        ⏱ This code expires in <strong>{expiryMinutes} minutes</strong>. Do not share it with anyone.
      </p>
    </td></tr>
    </table>

    <p style=""margin:28px 0 0;color:{TextSecondary};font-size:13px;line-height:1.5;"">
      If you didn't request this code, you can safely ignore this email. Your account remains secure.
    </p>
  </td></tr>

  <!-- Footer -->
  <tr><td style=""background:#f8fafc;padding:20px 40px;border-top:1px solid #e5e7eb;text-align:center;"">
    <p style=""margin:0;color:#9ca3af;font-size:12px;"">
      © {DateTime.UtcNow.Year} SkyHorizon Airlines. All rights reserved.<br/>
      This is an automated email — please do not reply.
    </p>
  </td></tr>

</table>
</td></tr>
</table>
</body>
</html>";
        }

        // ─── Booking Confirmation Email Template ────────────────────────

        public static string BookingConfirmationTemplate(string userName, BookingEmailModel booking)
        {
            var passengerRows = string.Join("", booking.Passengers.Select((p, i) =>
                $@"<tr style=""border-bottom:1px solid #f0f0f0;"">
                    <td style=""padding:10px 12px;color:{TextPrimary};font-size:14px;"">{i + 1}</td>
                    <td style=""padding:10px 12px;color:{TextPrimary};font-size:14px;font-weight:600;"">{p.Name}</td>
                    <td style=""padding:10px 12px;color:{TextSecondary};font-size:14px;text-align:center;"">{p.Age}</td>
                    <td style=""padding:10px 12px;text-align:center;"">
                      <span style=""background:{PrimaryColor};color:#fff;padding:4px 10px;border-radius:6px;font-size:13px;font-weight:600;"">{p.SeatNo}</span>
                    </td>
                  </tr>"));

            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f0f4f8;font-family:'Segoe UI',Roboto,Arial,sans-serif;"">
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color:#f0f4f8;padding:40px 20px;"">
<tr><td align=""center"">
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""600"" style=""max-width:600px;background:{CardBg};border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">

  <!-- Header -->
  <tr><td style=""background:linear-gradient(135deg,{GradientStart},{GradientEnd});padding:32px 40px;text-align:center;"">
    <h1 style=""margin:0;color:#fff;font-size:28px;font-weight:800;letter-spacing:1px;"">✈ SkyHorizon</h1>
    <p style=""margin:8px 0 0;color:rgba(255,255,255,0.9);font-size:14px;"">Booking Confirmation</p>
  </td></tr>

  <!-- Success Banner -->
  <tr><td style=""padding:28px 40px 0;"">
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
    <tr><td style=""background:#D1FAE5;border-radius:12px;padding:16px 20px;text-align:center;"">
      <p style=""margin:0;color:#065F46;font-size:16px;font-weight:700;"">
        ✅ Your booking has been confirmed!
      </p>
    </td></tr>
    </table>
  </td></tr>

  <!-- Body -->
  <tr><td style=""padding:28px 40px 16px;"">
    <p style=""margin:0 0 20px;color:{TextPrimary};font-size:16px;"">Hello <strong>{userName}</strong>,</p>
    <p style=""margin:0 0 24px;color:{TextSecondary};font-size:14px;line-height:1.6;"">
      Thank you for choosing SkyHorizon. Here are your booking details:
    </p>

    <!-- PNR highlight -->
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin-bottom:20px;"">
    <tr><td align=""center"" style=""padding:16px;background:linear-gradient(135deg,{GradientStart}08,{GradientEnd}15);border:2px solid {PrimaryColor};border-radius:12px;"">
      <p style=""margin:0 0 4px;color:{TextSecondary};font-size:12px;text-transform:uppercase;letter-spacing:2px;"">Your PNR</p>
      <p style=""margin:0;color:{PrimaryColor};font-size:28px;font-weight:800;letter-spacing:6px;font-family:'Courier New',monospace;"">{booking.PNR}</p>
    </td></tr>
    </table>

    <!-- Flight Details -->
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background:#f8fafc;border-radius:12px;overflow:hidden;margin-bottom:20px;"">
    <tr>
      <td style=""padding:20px;width:45%;text-align:center;border-right:2px dashed #e5e7eb;"">
        <p style=""margin:0 0 4px;color:{TextSecondary};font-size:11px;text-transform:uppercase;letter-spacing:1px;"">From</p>
        <p style=""margin:0;color:{TextPrimary};font-size:22px;font-weight:800;"">{booking.Source}</p>
        <p style=""margin:6px 0 0;color:{TextSecondary};font-size:12px;"">{booking.DepartureTime:dd MMM yyyy}</p>
        <p style=""margin:2px 0 0;color:{PrimaryColor};font-size:14px;font-weight:600;"">{booking.DepartureTime:HH:mm}</p>
      </td>
      <td style=""padding:20px;width:10%;text-align:center;vertical-align:middle;"">
        <span style=""font-size:20px;"">✈️</span>
      </td>
      <td style=""padding:20px;width:45%;text-align:center;border-left:2px dashed #e5e7eb;"">
        <p style=""margin:0 0 4px;color:{TextSecondary};font-size:11px;text-transform:uppercase;letter-spacing:1px;"">To</p>
        <p style=""margin:0;color:{TextPrimary};font-size:22px;font-weight:800;"">{booking.Destination}</p>
        <p style=""margin:6px 0 0;color:{TextSecondary};font-size:12px;"">{booking.ArrivalTime:dd MMM yyyy}</p>
        <p style=""margin:2px 0 0;color:{PrimaryColor};font-size:14px;font-weight:600;"">{booking.ArrivalTime:HH:mm}</p>
      </td>
    </tr>
    <tr><td colspan=""3"" style=""padding:0 20px 16px;text-align:center;"">
      <p style=""margin:0;color:{TextSecondary};font-size:13px;"">Flight <strong style=""color:{TextPrimary};"">{booking.FlightNumber}</strong></p>
    </td></tr>
    </table>

    <!-- Passengers Table -->
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""border:1px solid #e5e7eb;border-radius:10px;overflow:hidden;margin-bottom:20px;"">
    <tr style=""background:#f1f5f9;"">
      <th style=""padding:10px 12px;color:{TextSecondary};font-size:11px;text-transform:uppercase;letter-spacing:1px;text-align:left;"">#</th>
      <th style=""padding:10px 12px;color:{TextSecondary};font-size:11px;text-transform:uppercase;letter-spacing:1px;text-align:left;"">Passenger</th>
      <th style=""padding:10px 12px;color:{TextSecondary};font-size:11px;text-transform:uppercase;letter-spacing:1px;text-align:center;"">Age</th>
      <th style=""padding:10px 12px;color:{TextSecondary};font-size:11px;text-transform:uppercase;letter-spacing:1px;text-align:center;"">Seat</th>
    </tr>
    {passengerRows}
    </table>

    <!-- Total Amount -->
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin-bottom:12px;"">
    <tr><td style=""background:{DarkBg};border-radius:10px;padding:16px 20px;"">
      <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
      <tr>
        <td style=""color:rgba(255,255,255,0.7);font-size:14px;"">Total Amount</td>
        <td style=""text-align:right;color:#fff;font-size:22px;font-weight:800;"">₹{booking.TotalAmount:N2}</td>
      </tr>
      </table>
    </td></tr>
    </table>
  </td></tr>

  <!-- Tips -->
  <tr><td style=""padding:0 40px 28px;"">
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
    <tr><td style=""background:#EFF6FF;border-left:4px solid {PrimaryColor};border-radius:8px;padding:14px 18px;"">
      <p style=""margin:0;color:#1E40AF;font-size:13px;line-height:1.5;"">
        💡 <strong>Next steps:</strong> Complete web check-in 24 hours before departure. Carry a valid photo ID. Arrive at the airport 2 hours before your scheduled departure.
      </p>
    </td></tr>
    </table>
  </td></tr>

  <!-- Footer -->
  <tr><td style=""background:#f8fafc;padding:20px 40px;border-top:1px solid #e5e7eb;text-align:center;"">
    <p style=""margin:0;color:#9ca3af;font-size:12px;"">
      © {DateTime.UtcNow.Year} SkyHorizon Airlines. All rights reserved.<br/>
      Need help? Contact support@aerogo.dev
    </p>
  </td></tr>

</table>
</td></tr>
</table>
</body>
</html>";
        }

        // ─── Booking Cancellation Email Template ────────────────────────

        public static string BookingCancellationTemplate(string userName, string pnr, string flightNumber, decimal refundAmount)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f0f4f8;font-family:'Segoe UI',Roboto,Arial,sans-serif;"">
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color:#f0f4f8;padding:40px 20px;"">
<tr><td align=""center"">
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""560"" style=""max-width:560px;background:{CardBg};border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);"">

  <!-- Header -->
  <tr><td style=""background:linear-gradient(135deg,#DC2626,#F87171);padding:32px 40px;text-align:center;"">
    <h1 style=""margin:0;color:#fff;font-size:28px;font-weight:800;letter-spacing:1px;"">✈ SkyHorizon</h1>
    <p style=""margin:8px 0 0;color:rgba(255,255,255,0.9);font-size:14px;"">Booking Cancellation</p>
  </td></tr>

  <!-- Body -->
  <tr><td style=""padding:36px 40px;"">
    <h2 style=""margin:0 0 8px;color:{TextPrimary};font-size:20px;"">Hello, {userName}</h2>
    <p style=""margin:0 0 24px;color:{TextSecondary};font-size:14px;line-height:1.6;"">
      Your booking has been cancelled as requested. Details below:
    </p>

    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background:#FEF2F2;border-radius:12px;padding:20px;margin-bottom:20px;"">
    <tr>
      <td style=""padding:8px 0;"">
        <span style=""color:{TextSecondary};font-size:13px;"">PNR</span><br/>
        <span style=""color:{TextPrimary};font-size:16px;font-weight:700;"">{pnr}</span>
      </td>
      <td style=""padding:8px 0;text-align:right;"">
        <span style=""color:{TextSecondary};font-size:13px;"">Flight</span><br/>
        <span style=""color:{TextPrimary};font-size:16px;font-weight:700;"">{flightNumber}</span>
      </td>
    </tr>
    </table>

    {(refundAmount > 0 ? $@"
    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background:#D1FAE5;border-radius:10px;padding:16px 20px;margin-bottom:16px;"">
    <tr>
      <td style=""color:#065F46;font-size:14px;"">Refund Amount</td>
      <td style=""text-align:right;color:#065F46;font-size:20px;font-weight:800;"">₹{refundAmount:N2}</td>
    </tr>
    </table>
    <p style=""margin:0;color:{TextSecondary};font-size:13px;"">Your refund will be processed within 5–7 business days.</p>
    " : "")}
  </td></tr>

  <!-- Footer -->
  <tr><td style=""background:#f8fafc;padding:20px 40px;border-top:1px solid #e5e7eb;text-align:center;"">
    <p style=""margin:0;color:#9ca3af;font-size:12px;"">
      © {DateTime.UtcNow.Year} SkyHorizon Airlines. All rights reserved.
    </p>
  </td></tr>

</table>
</td></tr>
</table>
</body>
</html>";
        }
    }
}
