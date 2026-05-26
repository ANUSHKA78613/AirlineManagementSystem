using EventBus.Abstractions;
using EventBus.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Booking.API.ReadModel
{
    /// <summary>
    /// Background service that subscribes to BookingStatusChangedEvent,
    /// PaymentCompletedEvent, and PaymentFailedEvent via RabbitMQ and
    /// projects changes into the read-side database.
    ///
    /// This is the core of eventual consistency in the Booking service's CQRS pattern:
    /// - Commands write to the write-side DB (BookingDbContext)
    /// - Domain events propagate through RabbitMQ
    /// - This handler projects them into the read-side DB (BookingReadDbContext)
    /// - Queries read from the read-side DB for fast, denormalized access
    /// </summary>
    public class BookingProjectionHandler : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;

        public BookingProjectionHandler(IServiceProvider serviceProvider, IEventBus eventBus)
        {
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Information("[ReadModel] Booking projection handler starting — subscribing to domain events");

            _eventBus.Subscribe<BookingStatusChangedEvent>(HandleBookingStatusChanged);

            Log.Information("[ReadModel] Booking projection handler started successfully");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Information("[ReadModel] Booking projection handler stopping");
            return Task.CompletedTask;
        }

        private void HandleBookingStatusChanged(BookingStatusChangedEvent @event)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var readDb = scope.ServiceProvider.GetRequiredService<BookingReadDbContext>();

                var existing = readDb.BookingReadModels.FirstOrDefault(b => b.PNR == @event.PNR);

                if (existing != null)
                {
                    // Update existing projection
                    existing.Status = @event.Status;
                    existing.TotalAmount = @event.TotalAmount;
                    existing.PassengerCount = @event.PassengerCount;
                    existing.FlightNumber = @event.FlightNumber ?? existing.FlightNumber;
                    existing.Source = @event.Source ?? existing.Source;
                    existing.Destination = @event.Destination ?? existing.Destination;
                    existing.DepartureTime = @event.DepartureTime ?? existing.DepartureTime;
                    existing.LastUpdatedAt = DateTime.UtcNow;

                    Log.Information("[ReadModel] Updated booking projection: PNR={PNR} → Status={Status}",
                        @event.PNR, @event.Status);
                }
                else
                {
                    // Create new projection
                    readDb.BookingReadModels.Add(new BookingReadModel
                    {
                        PNR = @event.PNR,
                        UserId = @event.UserId,
                        FlightId = @event.FlightId,
                        Status = @event.Status,
                        TotalAmount = @event.TotalAmount,
                        PassengerCount = @event.PassengerCount,
                        FlightNumber = @event.FlightNumber,
                        Source = @event.Source,
                        Destination = @event.Destination,
                        DepartureTime = @event.DepartureTime,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    });

                    Log.Information("[ReadModel] Created booking projection: PNR={PNR}, Status={Status}",
                        @event.PNR, @event.Status);
                }

                readDb.SaveChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ReadModel] Failed to project BookingStatusChangedEvent for PNR={PNR}", @event.PNR);
            }
        }
    }
}
