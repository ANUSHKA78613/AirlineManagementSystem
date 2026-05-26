using EventBus.Abstractions;
using EventBus.Events;
using Flight.Infrastructure.Persistence;
using Flight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flight.API.Events
{
    public class CouponUsedEventHandler : IIntegrationEventHandler<CouponUsedEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CouponUsedEventHandler> _logger;

        public CouponUsedEventHandler(IServiceScopeFactory scopeFactory, ILogger<CouponUsedEventHandler> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Handle(CouponUsedEvent @event)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FlightDbContext>();

            var coupon = await context.Coupons.FirstOrDefaultAsync(c => c.Code == @event.CouponCode);
            if (coupon != null && coupon.IsActive)
            {
                // Increment global usage count
                coupon.UsedCount += 1;

                // Record per-user usage for enforcement
                context.CouponUsages.Add(new CouponUsage
                {
                    CouponCode = @event.CouponCode,
                    UserId = @event.UserId,
                    PNR = @event.PNR,
                    UsedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
                _logger.LogInformation($"Coupon {@event.CouponCode} usage recorded for UserId: {@event.UserId}, PNR: {@event.PNR}. Global count: {coupon.UsedCount}");
            }
        }
    }
}
