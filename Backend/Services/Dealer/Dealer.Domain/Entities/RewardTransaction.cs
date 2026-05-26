using System;

namespace Dealer.Domain.Entities
{
    public class RewardTransaction
    {
        public int TxnId { get; set; }
        public int RewardId { get; set; }
        public decimal Points { get; set; }
        public string Type { get; set; } // Earn, Redeem
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
