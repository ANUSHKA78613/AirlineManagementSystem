using System;
namespace Dealer.Domain.Entities
{
    public class RewardAccount
    {
        public int RewardId { get; set; }
        public int UserId { get; set; }
        public string MembershipTier { get; set; } // Silver, Gold, Platinum
        public decimal TotalPoints { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public RewardAccount() { EnrollmentDate = DateTime.UtcNow; MembershipTier = "Silver"; TotalPoints = 0; }
    }
}
