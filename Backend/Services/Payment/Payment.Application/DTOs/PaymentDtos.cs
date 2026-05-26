namespace Payment.Application.DTOs
{
    public class ProcessPaymentDto
    {
        public string PNR { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Razorpay";
    }
}
