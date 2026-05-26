using System;

namespace Operations.Domain.Entities
{
    public class Baggage
    {
        public int BaggageId { get; set; }
        public int PassengerId { get; set; }
        public string PNR { get; set; } = string.Empty;
        public string TagNumber { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        // Status: Checked, Overweight, Lost, Found, Delivered
        public string Status { get; set; } = "Checked";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
