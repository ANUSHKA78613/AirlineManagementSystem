using EventBus.Abstractions;
using EventBus.Events;
using Payment.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Payment.API.Saga
{
    public class BookingCreatedEventHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;

        public BookingCreatedEventHandler(IServiceProvider serviceProvider, IEventBus eventBus)
        {
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
        }

        public void Handle(BookingCreatedEvent @event)
        {
            Console.WriteLine($"[Saga] Correlation {@event.CorrelationId}: Received BookingCreatedEvent for PNR: {@event.PNR}");

            try
            {
                // We no longer auto-pay here. The user must manually pay via Razorpay.
                // We just log that we are anticipating payment.
                Console.WriteLine($"[Saga] Anticipating payment for Booking PNR: {@event.PNR}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Saga] Payment FAILED for PNR: {@event.PNR}: {ex.Message}");

                // Compensating transaction: Publish PaymentFailedEvent
                _eventBus.Publish(new PaymentFailedEvent
                {
                    CorrelationId = @event.CorrelationId,
                    PNR = @event.PNR,
                    Reason = ex.Message,
                    UserId = @event.UserId,
                    FlightId = @event.FlightId
                });
            }
        }
    }
}
