using EventBus.Abstractions;
using EventBus.Events;
using Flight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Flight.API.Events
{
    public class SeatInventoryEventHandler : IIntegrationEventHandler<SeatInventoryChangedEvent>, IIntegrationEventHandler<TicketCancelledEvent>
    {
        private readonly IServiceProvider _serviceProvider;

        public SeatInventoryEventHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task Handle(SeatInventoryChangedEvent @event)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FlightDbContext>();

            var flight = await context.Flights.FindAsync(@event.FlightId);
            
            System.IO.Directory.CreateDirectory("logs");
            System.IO.File.AppendAllText("logs/SeatDebug.txt", $"[SeatEvent] Received Event! FlightId: {@event.FlightId}, Booked: {string.Join(",", @event.BookedSeatNumbers)}\n");

            if (flight != null)
            {
                // This means seats are booked, available seats reduce
                flight.AvailableSeats -= @event.SeatsBooked;
                flight.AvailableSeats += @event.SeatsReleased;
                
                if (@event.BookedSeatNumbers.Any())
                {
                    var normalizedBooked = @event.BookedSeatNumbers.Select(s => s.Split(' ').Last().Trim().ToUpperInvariant()).ToList();
                    var allSeats = await context.Seats.Where(s => s.FlightId == @event.FlightId).ToListAsync();

                    var seatsToBook = allSeats.Where(s => normalizedBooked.Contains(s.SeatNo.Trim().ToUpperInvariant())).ToList();
                    foreach (var s in seatsToBook) s.IsAvailable = false;
                }

                if (@event.ReleasedSeatNumbers.Any())
                {
                    var normalizedReleased = @event.ReleasedSeatNumbers.Select(s => s.Split(' ').Last().Trim().ToUpperInvariant()).ToList();
                    var allSeats = await context.Seats.Where(s => s.FlightId == @event.FlightId).ToListAsync();

                    var seatsToRelease = allSeats.Where(s => normalizedReleased.Contains(s.SeatNo.Trim().ToUpperInvariant())).ToList();
                    foreach (var s in seatsToRelease) s.IsAvailable = true;
                }

                await context.SaveChangesAsync();
                Log.Information("Flight {FlightId} available seats updated. Booked: {Booked}, Released: {Released}. New Available: {Available}", 
                    @event.FlightId, @event.SeatsBooked, @event.SeatsReleased, flight.AvailableSeats);
            }
        }

        public async Task Handle(TicketCancelledEvent @event)
        {
            // NOTE: CancelBookingCommandHandler publishes BOTH TicketCancelledEvent AND SeatInventoryChangedEvent.
            // SeatInventoryChangedEvent already handles AvailableSeats count + individual seat flags.
            // This handler should ONLY handle refund-related side effects for Flight service.
            // We do NOT modify AvailableSeats here to avoid double-counting.
            
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FlightDbContext>();

            var flight = await context.Flights.FindAsync(@event.FlightId);
            if (flight != null)
            {
                // Only release individual seat flags if they weren't already released by SeatInventoryChangedEvent
                // This is a safety net — check if seats are still marked as unavailable before releasing
                if (@event.ReleasedSeatNumbers.Any())
                {
                    var normalizedReleased = @event.ReleasedSeatNumbers.Select(s => s.Split(' ').Last().Trim().ToUpperInvariant()).ToList();
                    var allSeats = await context.Seats.Where(s => s.FlightId == @event.FlightId).ToListAsync();

                    var seatsToRelease = allSeats
                        .Where(s => normalizedReleased.Contains(s.SeatNo.Trim().ToUpperInvariant()) && !s.IsAvailable)
                        .ToList();
                    
                    if (seatsToRelease.Any())
                    {
                        foreach (var s in seatsToRelease) s.IsAvailable = true;
                        await context.SaveChangesAsync();
                        Log.Information("Flight {FlightId}: TicketCancelledEvent released {Count} remaining seat flags", 
                            @event.FlightId, seatsToRelease.Count);
                    }
                }

                Log.Information("Flight {FlightId} TicketCancelledEvent processed. PNR: {PNR}, SeatsReleased: {Released}", 
                    @event.FlightId, @event.PNR, @event.SeatsReleased);
            }
        }
    }
}
