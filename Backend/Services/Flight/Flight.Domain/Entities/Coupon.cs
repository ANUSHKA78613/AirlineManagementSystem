using System;

namespace Flight.Domain.Entities
{
    public class Coupon
    {
        public int CouponId { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; }     // 1–100
        public bool IsActive { get; set; } = true;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMonths(1);
        public int UsageLimit { get; set; } = 100;
        public int UsedCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
