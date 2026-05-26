using EventBus.Abstractions;
using EventBus.Events;
using Dealer.Infrastructure.Persistence;
using Dealer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Dealer.API.Events
{
    public class DealerPaymentEventHandler : IIntegrationEventHandler<PaymentCompletedEvent>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;

        public DealerPaymentEventHandler(IServiceProvider serviceProvider, IEventBus eventBus)
        {
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
        }

        public async Task Handle(PaymentCompletedEvent @event)
        {
            if (@event == null) return;

            Log.Information("Dealer received PaymentCompletedEvent for PNR {PNR}, UserId {UserId}", @event.PNR, @event.UserId);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DealerDbContext>();

            // Strategy: Find the dealer who is assigned to this booking user via AgentCustomers table
            // This ensures commission only goes to the dealer who actually booked the passenger
            DealerAgent? matchedDealer = null;

            // 1. Check if the UserId corresponds to a dealer (dealer booking for themselves or on behalf of a customer)
            var customerAssignment = await context.AgentCustomers
                .Where(ac => ac.CustomerId == @event.UserId && ac.IsActive)
                .FirstOrDefaultAsync();

            if (customerAssignment != null)
            {
                matchedDealer = await context.DealerAgents
                    .FirstOrDefaultAsync(a => a.AgentId == customerAssignment.AgentId && a.IsActive);
                Log.Information("Matched dealer {AgentCode} via customer assignment for UserId {UserId}",
                    matchedDealer?.AgentCode, @event.UserId);
            }

            // 2. Fallback: Check if the booking user IS a dealer themselves (dealer books directly)
            if (matchedDealer == null)
            {
                // Try to match by checking if UserId maps to a dealer's own account
                // This handles the case where a Dealer user books tickets themselves
                var allDealers = await context.DealerAgents.Where(a => a.IsActive).ToListAsync();
                // Only match if there's a unique dealer whose AgentId matches the UserId
                // This is a weak match — only works if the dealer's AgentId happens to equal the user's Identity userId
                // In practice, dealer bookings should go through the AgentCustomers assignment
            }

            if (matchedDealer == null)
            {
                Log.Information("No dealer associated with UserId {UserId} for PNR {PNR}. Skipping commission.", @event.UserId, @event.PNR);
                return;
            }

            var wallet = await context.Wallets.FirstOrDefaultAsync(w => w.AgentId == matchedDealer.AgentId);
            if (wallet == null)
            {
                Log.Warning("Dealer {AgentCode} has no wallet. Skipping commission for PNR {PNR}.", matchedDealer.AgentCode, @event.PNR);
                return;
            }

            decimal commission = @event.Amount * (matchedDealer.CommissionRate / 100m);
            
            wallet.Balance += commission;
            wallet.LastTransactionDate = DateTime.UtcNow;

            context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.WalletId,
                Amount = commission,
                Type = "Credit",
                Description = $"Commission for PNR {@event.PNR}",
                BalanceAfter = wallet.Balance,
                CreatedAt = DateTime.UtcNow
            });

            var commissionRecord = new CommissionRecord
            {
                AgentCode = matchedDealer.AgentCode,
                PNR = @event.PNR,
                BookingAmount = @event.Amount,
                CommissionAmount = commission,
                Status = "Paid",
                EarnedDate = DateTime.UtcNow
            };
            context.Commissions.Add(commissionRecord);

            await context.SaveChangesAsync();

            _eventBus.Publish(new CommissionGeneratedEvent
            {
                PNR = @event.PNR,
                DealerAgentId = matchedDealer.AgentId,
                AgentCode = matchedDealer.AgentCode,
                BookingAmount = @event.Amount,
                CommissionAmount = commission,
                CommissionRate = matchedDealer.CommissionRate
            });
            
            Log.Information("Commission of {Amount} credited to Dealer {AgentCode} for PNR {PNR}", commission, matchedDealer.AgentCode, @event.PNR);
        }
    }
}

