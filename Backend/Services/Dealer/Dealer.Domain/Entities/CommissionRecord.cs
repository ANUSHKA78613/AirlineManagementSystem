using System;
namespace Dealer.Domain.Entities
{
    public class CommissionRecord
    {
        public int CommissionId { get; set; }
        public int AgentId { get; set; }
        public string AgentCode { get; set; } = string.Empty;
        public decimal BookingAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public string Status { get; set; } // Pending, Paid, Reversed
        public DateTime EarnedDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? PNR { get; set; }
        public bool IsReversed { get; set; } = false;
        public DateTime? ReversedAt { get; set; }
        public CommissionRecord() { EarnedDate = DateTime.UtcNow; Status = "Pending"; }
    }
}
