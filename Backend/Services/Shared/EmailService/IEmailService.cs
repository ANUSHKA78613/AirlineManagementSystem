namespace Shared.EmailService
{
    /// <summary>
    /// Email service interface for sending transactional emails across all microservices.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>Send an OTP verification email with a styled HTML template.</summary>
        Task SendOtpEmailAsync(string toEmail, string userName, string otpCode, int expiryMinutes = 1);

        /// <summary>Send a booking confirmation email with PNR, flight, and passenger details.</summary>
        Task SendBookingConfirmationAsync(string toEmail, string userName, BookingEmailModel booking);

        /// <summary>Send a booking cancellation email.</summary>
        Task SendBookingCancellationAsync(string toEmail, string userName, string pnr, string flightNumber, decimal refundAmount);

        /// <summary>Send a generic transactional email (subject + HTML body).</summary>
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);

        /// <summary>Send a boarding pass PDF via email.</summary>
        Task SendBoardingPassAsync(string toEmail, string userName, string pnr, byte[] pdfAttachment);
    }

    /// <summary>Model for booking confirmation email data.</summary>
    public class BookingEmailModel
    {
        public string PNR { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Confirmed";
        public List<PassengerEmailModel> Passengers { get; set; } = new();
    }

    public class PassengerEmailModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string SeatNo { get; set; } = string.Empty;
    }
}
