using System;

namespace Dealer.Domain.Entities
{
    public class DealerWallet
    {
        public int WalletId { get; set; }
        public int AgentId { get; set; }
        public decimal Balance { get; set; } = 0m;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastTransactionDate { get; set; }

        // Navigation property
        public DealerAgent Agent { get; set; } = null!;
    }

    public class WalletTransaction
    {
        public int TransactionId { get; set; }
        public int WalletId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;  // Credit, Debit
        public string Description { get; set; } = string.Empty;
        public decimal BalanceAfter { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public DealerWallet Wallet { get; set; } = null!;
    }
}
