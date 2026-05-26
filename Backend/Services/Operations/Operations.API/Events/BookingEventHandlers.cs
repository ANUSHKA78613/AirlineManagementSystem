using EventBus.Abstractions;
using EventBus.Events;
using Operations.Infrastructure.Persistence;
using Shared.EmailService;
using Operations.Domain.Entities;
using Serilog;

namespace Operations.API.Events
{
    public class BookingStatusChangedEventHandler : IIntegrationEventHandler<BookingStatusChangedEvent>
    {
        private readonly IServiceProvider _serviceProvider;

        public BookingStatusChangedEventHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task Handle(BookingStatusChangedEvent @event)
        {
            if (@event == null) return;

            Log.Information("Operations received BookingStatusChangedEvent for PNR {PNR} with Status {Status}", @event.PNR, @event.Status);

            // In a real operations setting this would update flight manifests, 
            // calculate baggage prep requirements, etc.
            
            // Generate some passenger issues if it's confirmed
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();

            if (@event.Status == "Confirmed")
            {
                // Note: @event doesn't contain Email directly by default, but we have added it.
                var notification = new NotificationRecord
                {
                    UserId = @event.UserId,
                    Email = !string.IsNullOrEmpty(@event.UserEmail) ? @event.UserEmail : $"user{@event.UserId}@skyhorizon.com",
                    Subject = $"Booking Confirmed — PNR: {@event.PNR}",
                    Message = $"Flight {@event.FlightNumber ?? @event.FlightId.ToString()} from {@event.Source} to {@event.Destination}. Total Amount: ₹{@event.TotalAmount}",
                    Status = "Sent"
                };
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();
            }
        }
    }

    public class PaymentCompletedEventHandler : IIntegrationEventHandler<PaymentCompletedEvent>
    {
        private readonly IServiceProvider _serviceProvider;

        public PaymentCompletedEventHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task Handle(PaymentCompletedEvent @event)
        {
            if (@event == null) return;
            Log.Information("Operations received PaymentCompletedEvent for PNR {PNR}. Passengers ready for Boarding Pass generation.", @event.PNR);
            // Operations side-effects
        }
    }
}
