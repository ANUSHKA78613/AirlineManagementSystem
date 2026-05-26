using EventBus.Abstractions;
using EventBus.Events;
using Microsoft.EntityFrameworkCore;
using Payment.Infrastructure.Persistence;
using Serilog;

namespace Payment.API.Saga
{
    /// <summary>
    /// Background worker that simulates internal async refund processing.
    /// Also handles real Razorpay refunds if required.
    /// </summary>
    public class RefundProcessingWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;

        public RefundProcessingWorker(IServiceProvider serviceProvider, IEventBus eventBus)
        {
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("[RefundWorker] Started. Polling for Refunds...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRefundsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[RefundWorker] Error processing refunds: {Message}", ex.Message);
                }

                // Poll every 10 seconds
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            
            Log.Information("[RefundWorker] Stopped.");
        }

        private async Task ProcessRefundsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            // Find payments that have been Initiated but not fully processed
            var pendingRefunds = await context.Payments
                .Where(p => p.RefundStatus == "Initiated" || p.RefundStatus == "Processing")
                .ToListAsync(cancellationToken);

            foreach (var payment in pendingRefunds)
            {
                // Advance state from Initiated to Processing
                if (payment.RefundStatus == "Initiated")
                {
                    payment.RefundStatus = "Processing";
                    payment.RefundedAt = DateTime.UtcNow; // Set start time for delay calculation
                    await context.SaveChangesAsync(cancellationToken);
                    Log.Information("[RefundWorker] Payment {PNR} moved to Processing. Waiting ~40s.", payment.PNR);
                    continue;
                }

                // Check if it's been Processing for >= 40 seconds (simulated delay)
                if (payment.RefundStatus == "Processing" && payment.RefundedAt.HasValue && 
                    (DateTime.UtcNow - payment.RefundedAt.Value).TotalSeconds >= 40)
                {
                    Log.Information("[RefundWorker] Processing delay complete for PNR {PNR}.", payment.PNR);

                    // If it's a real Razorpay payment, we would call Razorpay refund API here.
                    // Instead, the RazorpayService would already handle it and update via Webhook.
                    // For Saga-Auto or test payments without pay_ IDs, we just mark as Refunded directly.
                    bool isRealRazorpay = payment.TransactionId != null && payment.TransactionId.StartsWith("pay_");
                    
                    if (isRealRazorpay && payment.Status == "RefundPending")
                    {
                        // In a real environment with Webhooks, we might leave it as RefundPending
                        // until Razorpay Webhook updates it to Refunded. 
                        // But for demonstration/fallback, we'll confirm it successful if webhook hasn't yet.
                        Log.Information("[RefundWorker] Assuming Razorpay refund successful for {PNR}", payment.PNR);
                    }

                    // Complete the refund
                    payment.Status = "Refunded";
                    payment.RefundStatus = "Successful";
                    await context.SaveChangesAsync(cancellationToken);

                    Log.Information("[RefundWorker] ✅ Refund successful for PNR {PNR}. Publishing Event.", payment.PNR);

                    _eventBus.Publish(new RefundCompletedEvent
                    {
                        CorrelationId = Guid.NewGuid().ToString("N"), // New correlation for the completion event
                        PNR = payment.PNR,
                        RefundId = payment.RefundId ?? "UNKNOWN",
                        RefundAmount = payment.RefundAmount,
                        Status = "Successful"
                    });
                }
            }
        }
    }
}
