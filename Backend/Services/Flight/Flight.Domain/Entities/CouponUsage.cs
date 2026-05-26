using System;

namespace Flight.Domain.Entities
{
    /// <summary>
    /// Tracks per-user coupon usage. Each row = one usage of a coupon by a specific user.
    /// </summary>
    public class CouponUsage
    {
        public int CouponUsageId { get; set; }
        public string CouponCode { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string PNR { get; set; } = string.Empty;
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    }
}
