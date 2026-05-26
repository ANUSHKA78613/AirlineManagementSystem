using EventBus.Abstractions;
using EventBus.Events;
using Dealer.Infrastructure.Persistence;
using Dealer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Dealer.API.Events
{
    public class DealerCancellationEventHandler : IIntegrationEventHandler<BookingCancelledEvent>
    {
        private readonly IServiceProvider _serviceProvider;

        public DealerCancellationEventHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task Handle(BookingCancelledEvent @event)
        {
            if (@event == null) return;

            Log.Information("Dealer received TicketCancelledEvent for PNR {PNR}, RefundAmount {Refund}", @event.PNR, @event.RefundAmount);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DealerDbContext>();

            // Find the dealer who made this booking by checking the AgentCustomers mapping
            var customerMapping = await context.AgentCustomers
                .FirstOrDefaultAsync(c => c.CustomerId == @event.UserId);

            DealerAgent dealer = null;
            if (customerMapping != null)
            {
                dealer = await context.DealerAgents
                    .FirstOrDefaultAsync(a => a.IsActive && a.AgentId == customerMapping.AgentId);
            }

            if (dealer == null)
            {
                // Try fallback — check if any dealer has a commission record for this PNR
                var commissionRecord = await context.Commissions
                    .Where(c => c.PNR == @event.PNR && !c.IsReversed)
                    .FirstOrDefaultAsync();

                if (commissionRecord != null)
                {
                    dealer = await context.DealerAgents
                        .Where(a => a.AgentCode == commissionRecord.AgentCode)
                        .FirstOrDefaultAsync();
                }
            }

            if (dealer == null)
            {
                Log.Warning("No dealer found for cancelled PNR {PNR} (UserId: {UserId}). Skipping refund.", @event.PNR, @event.UserId);
                return;
            }

            var wallet = await context.Wallets.FirstOrDefaultAsync(w => w.AgentId == dealer.AgentId);
            if (wallet == null)
            {
                Log.Warning("No wallet found for dealer {AgentCode}. Cannot process refund for PNR {PNR}.", dealer.AgentCode, @event.PNR);
                return;
            }

            // Credit the refund amount back to the dealer's wallet
            wallet.Balance += @event.RefundAmount;
            wallet.LastTransactionDate = DateTime.UtcNow;

            context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.WalletId,
                Amount = @event.RefundAmount,
                Type = "Credit",
                Description = $"Refund for cancelled PNR {@event.PNR}",
                BalanceAfter = wallet.Balance,
                CreatedAt = DateTime.UtcNow
            });

            // Reverse the commission record for this PNR (if one exists)
            var existingCommission = await context.Commissions
                .Where(c => c.PNR == @event.PNR && c.AgentCode == dealer.AgentCode && !c.IsReversed)
                .FirstOrDefaultAsync();

            if (existingCommission != null)
            {
                existingCommission.IsReversed = true;
                existingCommission.ReversedAt = DateTime.UtcNow;
                existingCommission.Status = "Reversed";

                // Deduct the commission that was previously credited
                wallet.Balance -= existingCommission.CommissionAmount;

                context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.WalletId,
                    Amount = existingCommission.CommissionAmount,
                    Type = "Debit",
                    Description = $"Commission reversal for cancelled PNR {@event.PNR}",
                    BalanceAfter = wallet.Balance,
                    CreatedAt = DateTime.UtcNow
                });

                Log.Information("Commission of {Amount} reversed for PNR {PNR} (Agent: {AgentCode})",
                    existingCommission.CommissionAmount, @event.PNR, dealer.AgentCode);
            }

            await context.SaveChangesAsync();

            Log.Information("Refund of {Amount} credited to dealer {AgentCode} wallet for PNR {PNR}. New balance: {Balance}",
                @event.RefundAmount, dealer.AgentCode, @event.PNR, wallet.Balance);
        }
    }
}
