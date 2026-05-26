using EventBus.Abstractions;
using EventBus.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Flight.API.ReadModel
{
    /// <summary>
    /// Background service that subscribes to FlightUpdatedEvent and SeatInventoryChangedEvent
    /// via RabbitMQ and projects changes into the read-side database.
    /// 
    /// This is the core of eventual consistency in the Flight service's CQRS pattern:
    /// - Commands write to the write-side DB (FlightDbContext)
    /// - Domain events propagate through RabbitMQ
    /// - This handler projects them into the read-side DB (FlightReadDbContext)
    /// - Queries read from the read-side DB for fast, denormalized access
    /// </summary>
    public class FlightProjectionHandler : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;

        public FlightProjectionHandler(IServiceProvider serviceProvider, IEventBus eventBus)
        {
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Information("[ReadModel] Flight projection handler starting — subscribing to domain events");

            _eventBus.Subscribe<FlightUpdatedEvent>(HandleFlightUpdated);
            _eventBus.Subscribe<SeatInventoryChangedEvent>(HandleSeatInventoryChanged);

            Log.Information("[ReadModel] Flight projection handler started successfully");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Information("[ReadModel] Flight projection handler stopping");
            return Task.CompletedTask;
        }

        private void HandleFlightUpdated(FlightUpdatedEvent @event)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var readDb = scope.ServiceProvider.GetRequiredService<FlightReadDbContext>();

                var existing = readDb.FlightReadModels.FirstOrDefault(f => f.FlightId == @event.FlightId);

                if (existing != null)
                {
                    // Update existing projection
                    existing.FlightNumber = @event.FlightNumber;
                    existing.Source = @event.Source;
                    existing.Destination = @event.Destination;
                    existing.DepartureTime = @event.DepartureTime;
                    existing.ArrivalTime = @event.ArrivalTime;
                    existing.Status = @event.Status;
                    existing.Price = 0; // Price is now computed dynamically from FlightPricings
                    existing.AvailableSeats = @event.AvailableSeats;
                    existing.GateNumber = @event.GateNumber;
                    existing.LastUpdatedAt = DateTime.UtcNow;

                    Log.Information("[ReadModel] Updated flight projection: {FlightNumber} (ID={FlightId})",
                        @event.FlightNumber, @event.FlightId);
                }
                else
                {
                    // Create new projection
                    readDb.FlightReadModels.Add(new FlightReadModel
                    {
                        FlightId = @event.FlightId,
                        FlightNumber = @event.FlightNumber,
                        Source = @event.Source,
                        Destination = @event.Destination,
                        DepartureTime = @event.DepartureTime,
                        ArrivalTime = @event.ArrivalTime,
                        Status = @event.Status,
                        Price = 0, // Price is now computed dynamically from FlightPricings
                        AvailableSeats = @event.AvailableSeats,
                        GateNumber = @event.GateNumber,
                        LastUpdatedAt = DateTime.UtcNow
                    });

                    Log.Information("[ReadModel] Created flight projection: {FlightNumber} (ID={FlightId})",
                        @event.FlightNumber, @event.FlightId);
                }

                readDb.SaveChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ReadModel] Failed to project FlightUpdatedEvent for FlightId={FlightId}", @event.FlightId);
            }
        }

        private void HandleSeatInventoryChanged(SeatInventoryChangedEvent @event)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var readDb = scope.ServiceProvider.GetRequiredService<FlightReadDbContext>();

                var existing = readDb.FlightReadModels.FirstOrDefault(f => f.FlightId == @event.FlightId);
                if (existing != null)
                {
                    existing.AvailableSeats = existing.AvailableSeats - @event.SeatsBooked + @event.SeatsReleased;
                    if (existing.AvailableSeats < 0) existing.AvailableSeats = 0;
                    existing.LastUpdatedAt = DateTime.UtcNow;
                    readDb.SaveChanges();

                    Log.Information("[ReadModel] Seat inventory updated for FlightId={FlightId}: Available={AvailableSeats}",
                        @event.FlightId, existing.AvailableSeats);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ReadModel] Failed to project SeatInventoryChangedEvent for FlightId={FlightId}", @event.FlightId);
            }
        }
    }
}
