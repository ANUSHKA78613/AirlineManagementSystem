using System;

namespace Payment.Domain.Entities
{
    public class Payment
    {
        public int PaymentId { get; set; }
        public string PNR { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } // Success, Failed
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public string? RefundId { get; set; }
        public decimal RefundAmount { get; set; }
        public DateTime? RefundedAt { get; set; }
        public string RefundStatus { get; set; } // None, Initiated, Processing, Successful, Failed
        public DateTime CreatedAt { get; set; }

        public Payment()
        {
            CreatedAt = DateTime.UtcNow;
            Status = "Pending";
            RefundStatus = "None";
            TransactionId = $"TXN{DateTime.UtcNow.Ticks % 100000000}";
        }
    }
}
