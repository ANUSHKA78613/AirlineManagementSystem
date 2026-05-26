using EventBus.Abstractions;
using EventBus.Events;
using Booking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.API.Saga
{
    public class PaymentResultEventHandler
    {
        private readonly IServiceProvider _serviceProvider;

        public PaymentResultEventHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void HandlePaymentCompleted(PaymentCompletedEvent @event)
        {
            Console.WriteLine($"[Saga] PaymentCompleted for PNR: {@event.PNR}, TxnId: {@event.TransactionId}");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<Shared.EmailService.IEmailService>();

                var booking = context.Bookings.FirstOrDefault(b => b.PNR == @event.PNR);
                if (booking != null)
                {
                    if (booking.Status == "Confirmed" || booking.Status == "Cancelled")
                    {
                        Console.WriteLine($"[Saga-Booking] Booking {booking.PNR} is already {booking.Status}. Idempotency skip.");
                        return;
                    }

                    booking.Status = "Confirmed";
                    context.SaveChanges();
                    
                    var passengers = context.Passengers.Where(p => p.PNR == @event.PNR).ToList();
                    var emailModel = new Shared.EmailService.BookingEmailModel
                    {
                        PNR = booking.PNR,
                        FlightNumber = "FLY-" + booking.FlightId,
                        TotalAmount = booking.TotalAmount,
                        Status = "Confirmed"
                    };

                    foreach(var p in passengers) {
                        emailModel.Passengers.Add(new Shared.EmailService.PassengerEmailModel {
                            Name = p.Name,
                            Age = p.Age,
                            SeatNo = p.SeatNo ?? "TBD"
                        });
                    }

                    var primaryContactEmail = booking.UserEmail ?? "operations@skyhorizon.com"; 
                    var customerName = booking.UserName ?? emailModel.Passengers.FirstOrDefault()?.Name ?? "Customer";

                    try {
                        emailService.SendBookingConfirmationAsync(primaryContactEmail, customerName, emailModel)
                            .GetAwaiter().GetResult();
                    } catch(Exception ex) {
                        Console.WriteLine($"[Saga Email Error]: {ex.Message}");
                    }

                    // Publish Eventual Consistency Events
                    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                    eventBus.Publish(new BookingStatusChangedEvent
                    {
                        PNR = booking.PNR,
                        UserId = booking.UserId,
                        FlightId = booking.FlightId,
                        Status = "Confirmed",
                        TotalAmount = booking.TotalAmount,
                        PassengerCount = passengers.Count
                    });

                    Console.WriteLine($"[Saga] Booking {booking.PNR} → Confirmed ✅ and Email dispatched to {primaryContactEmail}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Saga] ERROR in HandlePaymentCompleted for PNR {@event.PNR}: {ex.Message}");
            }
        }

        public void HandlePaymentFailed(PaymentFailedEvent @event)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var _eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

                Console.WriteLine($"[Saga-Booking] Correlation {@event.CorrelationId}: HandlePaymentFailed for PNR: {@event.PNR}");

                var booking = context.Bookings.FirstOrDefault(b => b.PNR == @event.PNR);
                if (booking != null)
                {
                    if (booking.Status == "Cancelled")
                    {
                        Console.WriteLine($"[Saga-Booking] Booking {booking.PNR} is already Cancelled. Skipping.");
                        return;
                    }

                    booking.Status = "Cancelled";
                    
                    var passengers = context.Passengers.Where(p => p.PNR == @event.PNR).ToList();
                    var bookedSeats = passengers.Select(p => p.SeatNo).Where(s => !string.IsNullOrEmpty(s)).ToList();

                    context.SaveChanges();
                    Console.WriteLine($"[Saga-Booking] Updated Booking {booking.PNR} status to Cancelled.");

                    // Publish Eventual Consistency Events
                    _eventBus.Publish(new BookingStatusChangedEvent
                    {
                        CorrelationId = @event.CorrelationId,
                        PNR = booking.PNR,
                        UserId = booking.UserId,
                        FlightId = booking.FlightId,
                        Status = booking.Status,
                        TotalAmount = booking.TotalAmount,
                        PassengerCount = passengers.Count,
                        UserEmail = booking.UserEmail
                    });

                    _eventBus.Publish(new SeatInventoryChangedEvent
                    {
                        CorrelationId = @event.CorrelationId,
                        FlightId = booking.FlightId,
                        SeatsBooked = 0,
                        SeatsReleased = passengers.Count,
                        BookedSeatNumbers = new List<string>(),
                        ReleasedSeatNumbers = bookedSeats
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Saga-Booking] Error processing PaymentFailedEvent: {ex.Message}");
            }
        }

        public void HandleRefundCompleted(RefundCompletedEvent @event)
        {
            Console.WriteLine($"[Saga-Booking] Correlation {@event.CorrelationId}: RefundCompleted for PNR: {@event.PNR}, RefundId: {@event.RefundId}, Status: {@event.Status}");
            // In a more complex architecture, we might update a 'RefundStatus' column in the read-model or notify the user here.
        }
    }
}
