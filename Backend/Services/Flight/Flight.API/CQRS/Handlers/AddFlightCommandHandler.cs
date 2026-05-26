using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Flight.Application.CQRS.Commands;
using Flight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EventBus.Abstractions;
using EventBus.Events;
using Shared.Middleware.Exceptions;

namespace Flight.API.CQRS.Handlers
{
    public class AddFlightCommandHandler : IRequestHandler<AddFlightCommand, string>
    {
        private readonly FlightDbContext _context;
        private readonly IEventBus _eventBus;

        public AddFlightCommandHandler(FlightDbContext context, IEventBus eventBus)
        {
            _context = context;
            _eventBus = eventBus;
        }

        public async Task<string> Handle(AddFlightCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;

            if (dto.Source == dto.Destination) throw new BadRequestException("Source and destination cannot be the same.");
            if (dto.DepartureTime >= dto.ArrivalTime) throw new BadRequestException("Departure must be before arrival.");
            if (dto.EconomySeats + dto.BusinessSeats + dto.FirstClassSeats != dto.TotalCapacity) throw new BadRequestException("The sum of all class seats must be equal to total capacity.");


            var sourceExists = await _context.Airports.AnyAsync(a => a.Code == dto.Source && a.IsActive, cancellationToken);
            if (!sourceExists) throw new NotFoundException("Airport", dto.Source);

            var destExists = await _context.Airports.AnyAsync(a => a.Code == dto.Destination && a.IsActive, cancellationToken);
            if (!destExists) throw new NotFoundException("Airport", dto.Destination);

            var existingFlight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightNumber == dto.FlightNumber, cancellationToken);
            
            Flight.Domain.Entities.Flight flight;

            if (existingFlight != null)
            {
                if (existingFlight.Status != "Deleted")
                    throw new ConflictException("Duplicate flight number. An active flight with this number already exists.");
                
                // Reactivate the deleted flight to avoid DB Unique Constraint on FlightNumber
                existingFlight.Status = "Scheduled";
                existingFlight.Source = dto.Source;
                existingFlight.Destination = dto.Destination;
                existingFlight.DepartureTime = dto.DepartureTime;
                existingFlight.ArrivalTime = dto.ArrivalTime;
                existingFlight.SourceTimeZone = dto.SourceTimeZone ?? "Asia/Kolkata";
                existingFlight.DestinationTimeZone = dto.DestinationTimeZone ?? "Asia/Kolkata";
                existingFlight.AircraftId = dto.AircraftId;
                flight = existingFlight;
            }
            else
            {
                flight = new Flight.Domain.Entities.Flight
                {
                    FlightNumber = dto.FlightNumber,
                    Source = dto.Source,
                    Destination = dto.Destination,
                    DepartureTime = dto.DepartureTime,
                    ArrivalTime = dto.ArrivalTime,
                    SourceTimeZone = dto.SourceTimeZone ?? "Asia/Kolkata",
                    DestinationTimeZone = dto.DestinationTimeZone ?? "Asia/Kolkata",
                    AircraftId = dto.AircraftId,
                    Status = "Scheduled"
                };
                _context.Flights.Add(flight);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Auto-generate pricing and capacities for 3 classes
            if (dto.EconomyPrice > 0)
            {
                var existingPrices = await _context.FlightPricings
                    .Where(p => p.FlightId == flight.FlightId)
                    .ToListAsync(cancellationToken);
                _context.FlightPricings.RemoveRange(existingPrices);

                _context.FlightPricings.Add(new Flight.Domain.Entities.FlightPricing {
                    FlightId = flight.FlightId,
                    Class = "Economy",
                    BasePrice = dto.EconomyPrice,
                    Multiplier = 1.0m,
                    WindowSeatCharge = dto.WindowSeatCharge,
                    IsActive = true
                });
                
                _context.FlightPricings.Add(new Flight.Domain.Entities.FlightPricing {
                    FlightId = flight.FlightId,
                    Class = "Business",
                    BasePrice = dto.BusinessPrice,
                    Multiplier = 1.0m,
                    WindowSeatCharge = dto.WindowSeatCharge,
                    IsActive = true
                });
                
                _context.FlightPricings.Add(new Flight.Domain.Entities.FlightPricing {
                    FlightId = flight.FlightId,
                    Class = "First",
                    BasePrice = dto.FirstClassPrice,
                    Multiplier = 1.0m,
                    WindowSeatCharge = dto.WindowSeatCharge,
                    IsActive = true
                });

                var existingConfigs = await _context.SeatConfigurations
                    .Where(c => c.FlightId == flight.FlightId)
                    .ToListAsync();
                _context.SeatConfigurations.RemoveRange(existingConfigs);

                _context.SeatConfigurations.Add(new Flight.Domain.Entities.SeatConfiguration {
                    FlightId = flight.FlightId,
                    Class = "Economy",
                    TotalCapacity = dto.EconomySeats,
                    TotalRows = dto.EconomySeats > 0 ? (int)Math.Ceiling(dto.EconomySeats / 6.0) : 0,
                    ColumnsLayout = "3-3"
                });
                
                _context.SeatConfigurations.Add(new Flight.Domain.Entities.SeatConfiguration {
                    FlightId = flight.FlightId,
                    Class = "Business",
                    TotalCapacity = dto.BusinessSeats,
                    TotalRows = dto.BusinessSeats > 0 ? (int)Math.Ceiling(dto.BusinessSeats / 4.0) : 0,
                    ColumnsLayout = "2-2"
                });

                _context.SeatConfigurations.Add(new Flight.Domain.Entities.SeatConfiguration {
                    FlightId = flight.FlightId,
                    Class = "First",
                    TotalCapacity = dto.FirstClassSeats,
                    TotalRows = dto.FirstClassSeats > 0 ? (int)Math.Ceiling(dto.FirstClassSeats / 4.0) : 0,
                    ColumnsLayout = "1-2-1"
                });

                flight.AvailableSeats = dto.TotalCapacity;

                await _context.SaveChangesAsync(cancellationToken);
            }

            // Publish domain event for eventual consistency (read-model projection)
            _eventBus.Publish(new FlightUpdatedEvent
            {
                FlightId = flight.FlightId,
                FlightNumber = flight.FlightNumber,
                Source = flight.Source,
                Destination = flight.Destination,
                DepartureTime = flight.DepartureTime,
                ArrivalTime = flight.ArrivalTime,
                Status = flight.Status,
                Price = dto.EconomyPrice > 0 ? dto.EconomyPrice : 0, // Fallback base price
                AvailableSeats = dto.TotalCapacity,
                GateNumber = flight.GateNumber
            });

            return $"Flight {dto.FlightNumber} added successfully.";
        }
    }
}
