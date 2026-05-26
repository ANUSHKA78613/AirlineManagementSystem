using EventBus.Abstractions;
using EventBus.Events;
using Payment.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Payment.API.Saga
{
    /// <summary>
    /// Handles BookingCancelledEvent to process refunds when bookings are cancelled.
    /// Scenario 2: Payment Success + User Cancellation -> Refund (Async)
    /// </summary>
    public class RefundEventHandler : IIntegrationEventHandler<BookingCancelledEvent>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;

        public RefundEventHandler(IServiceProvider serviceProvider, IEventBus eventBus)
        {
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
        }

        public async Task Handle(BookingCancelledEvent @event)
        {
            if (@event == null) return;

            Log.Information("[Refund] Correlation {CorrelationId}: BookingCancelledEvent for PNR {PNR}, RefundAmount: {Amount}", 
                @event.CorrelationId, @event.PNR, @event.RefundAmount);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            try
            {
                var payment = await context.Payments
                    .Where(p => p.PNR == @event.PNR && p.Status == "Success")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (payment == null)
                {
                    Log.Warning("[Refund] No successful payment found for PNR {PNR}. Skipping refund.", @event.PNR);
                    return;
                }

                // Idempotency Check
                if (payment.RefundStatus == "Initiated" || payment.RefundStatus == "Processing" || payment.RefundStatus == "Successful")
                {
                    Log.Information("[Refund] Payment for PNR {PNR} refund already in state: {RefundStatus}. Skipping.", @event.PNR, payment.RefundStatus);
                    return;
                }

                if (@event.RefundAmount <= 0)
                {
                    Log.Information("[Refund] RefundAmount is 0 for PNR {PNR}. No refund initiated.", @event.PNR);
                    return;
                }

                // Initialize Refund
                payment.Status = "RefundPending";
                payment.RefundStatus = "Initiated";
                payment.RefundAmount = @event.RefundAmount;
                payment.RefundId = $"REFUND-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
                
                await context.SaveChangesAsync();

                Log.Information("[Refund] Refund Initiated for PNR {PNR}, RefundId: {RefundId}", @event.PNR, payment.RefundId);

                _eventBus.Publish(new RefundInitiatedEvent
                {
                    CorrelationId = @event.CorrelationId,
                    PNR = payment.PNR,
                    RefundId = payment.RefundId,
                    RefundAmount = payment.RefundAmount,
                    TransactionId = payment.TransactionId
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Refund] ERROR processing refund for PNR {PNR}: {Message}", @event.PNR, ex.Message);
            }
        }
    }
}
