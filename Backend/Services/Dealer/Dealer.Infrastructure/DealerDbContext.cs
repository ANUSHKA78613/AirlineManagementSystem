using Microsoft.EntityFrameworkCore;
using Dealer.Domain.Entities;

namespace Dealer.Infrastructure.Persistence
{
    public class DealerDbContext : DbContext
    {
        public DealerDbContext(DbContextOptions<DealerDbContext> options) : base(options) { }

        // Dealer entities
        public DbSet<DealerAgent> DealerAgents { get; set; }
        public DbSet<AgentCustomer> AgentCustomers { get; set; }
        public DbSet<DealerWallet> Wallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }

        // Commission entities (merged)
        public DbSet<CommissionRecord> Commissions { get; set; }

        // Reward entities (merged)
        public DbSet<RewardAccount> RewardAccounts { get; set; }
        public DbSet<RewardTransaction> RewardTransactions { get; set; }

        // Analytics entities (merged)
        public DbSet<AnalyticsRecord> AnalyticsRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Dealer
            modelBuilder.Entity<DealerAgent>(entity =>
            {
                entity.HasKey(d => d.AgentId);
                entity.HasIndex(d => d.AgentCode).IsUnique();
                entity.Property(d => d.CommissionRate).HasColumnType("decimal(5,2)");
            });

            modelBuilder.Entity<AgentCustomer>(entity =>
            {
                entity.HasKey(ac => ac.AgentCustomerId);
                entity.HasOne(ac => ac.Agent)
                      .WithMany()
                      .HasForeignKey(ac => ac.AgentId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(ac => new { ac.AgentId, ac.CustomerId }).IsUnique();
            });

            modelBuilder.Entity<DealerWallet>(entity =>
            {
                entity.HasKey(w => w.WalletId);
                entity.HasIndex(w => w.AgentId).IsUnique();
                entity.Property(w => w.Balance).HasColumnType("decimal(18,2)");
                entity.HasOne(w => w.Agent)
                      .WithMany()
                      .HasForeignKey(w => w.AgentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WalletTransaction>(entity =>
            {
                entity.HasKey(t => t.TransactionId);
                entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");
                entity.Property(t => t.BalanceAfter).HasColumnType("decimal(18,2)");
                entity.HasOne(t => t.Wallet)
                      .WithMany()
                      .HasForeignKey(t => t.WalletId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Commission
            modelBuilder.Entity<CommissionRecord>(entity =>
            {
                entity.HasKey(c => c.CommissionId);
                entity.Property(c => c.BookingAmount).HasColumnType("decimal(18,2)");
                entity.Property(c => c.CommissionAmount).HasColumnType("decimal(18,2)");
            });

            // Reward
            modelBuilder.Entity<RewardAccount>(entity =>
            {
                entity.HasKey(r => r.RewardId);
                entity.Property(r => r.TotalPoints).HasColumnType("decimal(18,2)");
            });
            modelBuilder.Entity<RewardTransaction>(entity =>
            {
                entity.HasKey(r => r.TxnId);
                entity.Property(r => r.Points).HasColumnType("decimal(18,2)");
            });

            // Analytics
            modelBuilder.Entity<AnalyticsRecord>(entity =>
            {
                entity.HasKey(a => a.RecordId);
                entity.Property(a => a.Amount).HasColumnType("decimal(18,2)");
            });
        }
    }
}
