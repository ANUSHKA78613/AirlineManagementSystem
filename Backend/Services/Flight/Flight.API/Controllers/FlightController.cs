using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Flight.Application.DTOs;
using Flight.Application.CQRS.Commands;
using Flight.Application.CQRS.Queries;
using Flight.Infrastructure.Persistence;
using Flight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Middleware.Exceptions;

namespace Flight.API.Controllers
{
    [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    public class FlightController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly FlightDbContext _context;

        public FlightController(IMediator mediator, FlightDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        // ─── Flight CRUD ─────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> AddFlight([FromBody] AddFlightDto dto)
        {
            var sourceCode = dto.Source?.ToUpperInvariant();
            var destCode = dto.Destination?.ToUpperInvariant();

            var sourceExists = await _context.Airports.AnyAsync(a => a.Code == sourceCode && a.IsActive);
            if (!sourceExists) throw new NotFoundException("Airport", dto.Source ?? "null");

            var destExists = await _context.Airports.AnyAsync(a => a.Code == destCode && a.IsActive);
            if (!destExists) throw new NotFoundException("Airport", dto.Destination ?? "null");

            var existingFlight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightNumber == dto.FlightNumber);
            if (existingFlight != null)
            {
                if (existingFlight.Status != "Deleted")
                    throw new ConflictException($"Flight number '{dto.FlightNumber}' already exists.");
                else
                {
                    // Reactivate soft-deleted flight to bypass unique constraint
                    existingFlight.Source = dto.Source;
                    existingFlight.Destination = dto.Destination;
                    existingFlight.DepartureTime = dto.DepartureTime;
                    existingFlight.ArrivalTime = dto.ArrivalTime;
                    existingFlight.AircraftId = dto.AircraftId;
                    existingFlight.Status = "Scheduled";
                    
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Flight restored and successfully scheduled." });
                }
            }

            var result = await _mediator.Send(new AddFlightCommand(dto));
            return Ok(new { message = result });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAllFlights()
        {
            var flights = await _mediator.Send(new GetAllFlightsQuery());
            return Ok(flights);
        }

        [AllowAnonymous]
        [HttpGet("{flightId:int}")]
        public async Task<IActionResult> GetFlightById(int flightId)
        {
            var flight = await _context.Flights.AsNoTracking().FirstOrDefaultAsync(f => f.FlightId == flightId);
            if (flight == null) throw new NotFoundException("Flight", flightId);
            if (flight.DepartureTime < DateTime.UtcNow)
            {
                flight.Status = "Cancelled";
            }
            return Ok(flight);
        }

        [AllowAnonymous]
        [HttpGet("search")]
        public async Task<IActionResult> SearchFlights([FromQuery] SearchFlightDto dto)
        {
            var flights = await _mediator.Send(new SearchFlightsQuery(dto));
            return Ok(flights);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{flightId:int}")]
        public async Task<IActionResult> UpdateFlight(int flightId, [FromBody] UpdateFlightDto dto)
        {
            var flight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightId == flightId);
            if (flight == null) throw new NotFoundException("Flight", flightId);

            if (!string.IsNullOrWhiteSpace(dto.Source)) 
            {
                var exists = await _context.Airports.AnyAsync(a => a.Code == dto.Source.ToUpperInvariant() && a.IsActive);
                if (!exists) throw new NotFoundException("Airport", dto.Source);
            }
            if (!string.IsNullOrWhiteSpace(dto.Destination))
            {
                var exists = await _context.Airports.AnyAsync(a => a.Code == dto.Destination.ToUpperInvariant() && a.IsActive);
                if (!exists) throw new NotFoundException("Airport", dto.Destination);
            }

            var newDep = dto.DepartureTime ?? flight.DepartureTime;
            var newArr = dto.ArrivalTime ?? flight.ArrivalTime;

            if (newArr <= newDep) throw new BadRequestException("Arrival time must be after departure time.");

            if (!string.IsNullOrWhiteSpace(dto.Source)) flight.Source = dto.Source;
            if (!string.IsNullOrWhiteSpace(dto.Destination)) flight.Destination = dto.Destination;
            flight.DepartureTime = newDep;
            flight.ArrivalTime = newArr;
            if (!string.IsNullOrWhiteSpace(dto.GateNumber)) flight.GateNumber = dto.GateNumber;
            if (!string.IsNullOrWhiteSpace(dto.Status)) flight.Status = dto.Status;

            await _context.SaveChangesAsync();

            // Auto-update pricing if provided
            if (dto.EconomyPrice.HasValue && dto.EconomyPrice.Value > 0)
            {
                var existingPrices = await _context.FlightPricings
                    .Where(p => p.FlightId == flight.FlightId)
                    .ToListAsync();
                _context.FlightPricings.RemoveRange(existingPrices);

                _context.FlightPricings.Add(new FlightPricing {
                    FlightId = flight.FlightId,
                    Class = "Economy",
                    BasePrice = dto.EconomyPrice.Value,
                    Multiplier = 1.0m,
                    WindowSeatCharge = dto.WindowSeatCharge ?? 0m,
                    IsActive = true
                });
                
                _context.FlightPricings.Add(new FlightPricing {
                    FlightId = flight.FlightId,
                    Class = "Business",
                    BasePrice = dto.BusinessPrice ?? dto.EconomyPrice.Value,
                    Multiplier = 1.0m,
                    WindowSeatCharge = dto.WindowSeatCharge ?? 0m,
                    IsActive = true
                });
                
                _context.FlightPricings.Add(new FlightPricing {
                    FlightId = flight.FlightId,
                    Class = "First",
                    BasePrice = dto.FirstClassPrice ?? dto.EconomyPrice.Value,
                    Multiplier = 1.0m,
                    WindowSeatCharge = dto.WindowSeatCharge ?? 0m,
                    IsActive = true
                });

                var existingConfigs = await _context.SeatConfigurations
                    .Where(c => c.FlightId == flight.FlightId)
                    .ToListAsync();
                _context.SeatConfigurations.RemoveRange(existingConfigs);

                _context.SeatConfigurations.Add(new SeatConfiguration {
                    FlightId = flight.FlightId,
                    Class = "Economy",
                    TotalCapacity = dto.EconomySeats ?? 0,
                    TotalRows = dto.EconomySeats.GetValueOrDefault(0) > 0 ? (int)Math.Ceiling(dto.EconomySeats.Value / 6.0) : 0,
                    ColumnsLayout = "3-3"
                });
                
                _context.SeatConfigurations.Add(new SeatConfiguration {
                    FlightId = flight.FlightId,
                    Class = "Business",
                    TotalCapacity = dto.BusinessSeats ?? 0,
                    TotalRows = dto.BusinessSeats.GetValueOrDefault(0) > 0 ? (int)Math.Ceiling(dto.BusinessSeats.Value / 4.0) : 0,
                    ColumnsLayout = "2-2"
                });

                _context.SeatConfigurations.Add(new SeatConfiguration {
                    FlightId = flight.FlightId,
                    Class = "First",
                    TotalCapacity = dto.FirstClassSeats ?? 0,
                    TotalRows = dto.FirstClassSeats.GetValueOrDefault(0) > 0 ? (int)Math.Ceiling(dto.FirstClassSeats.Value / 4.0) : 0,
                    ColumnsLayout = "1-2-1"
                });

                if (dto.TotalCapacity.HasValue) flight.AvailableSeats = dto.TotalCapacity.Value;

                await _context.SaveChangesAsync();

                // Publish flight update for read model sync
                _mediator?.Send(new UpdateFlightStatusCommand(flight.FlightId, flight.Status));
            }

            return Ok(new { message = "Flight updated.", flight });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{flightId:int}")]
        public async Task<IActionResult> DeleteFlight(int flightId)
        {
            var flight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightId == flightId);
            if (flight == null) throw new NotFoundException("Flight", flightId);

            // Edge Case: Check for Active Bookings
            var hasActiveBookings = await _context.Seats.AnyAsync(s => s.FlightId == flightId && !s.IsAvailable);
            if (hasActiveBookings)
                throw new ConflictException("Cannot delete flight. There are active bookings (seats sold) for this flight. Please cancel the flight to issue refunds.");

            flight.Status = "Deleted";
            await _context.SaveChangesAsync();
            return Ok(new { message = "Flight soft-deleted.", flightId });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpPut("{flightId}/status")]
        public async Task<IActionResult> UpdateFlightStatus(int flightId, [FromQuery] string status)
        {
            var result = await _mediator.Send(new UpdateFlightStatusCommand(flightId, status));
            return Ok(new { message = result });
        }

        // ─── Routes (DB-backed) ─────────────────────────────────────

        [AllowAnonymous]
        [HttpGet("routes")]
        public async Task<IActionResult> GetRoutes()
        {
            var routes = await _context.Routes
                .Where(r => r.IsActive)
                .OrderBy(r => r.Source).ThenBy(r => r.Destination)
                .ToListAsync();
            return Ok(routes);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("routes")]
        public async Task<IActionResult> AddRoute([FromBody] Flight.Domain.Entities.Route route)
        {
            if (string.IsNullOrWhiteSpace(route.Source) || string.IsNullOrWhiteSpace(route.Destination))
                throw new BadRequestException("Source and destination are required.");
            if (route.Source.Equals(route.Destination, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Source and destination cannot be the same.");

            var source = route.Source.ToUpperInvariant();
            var destination = route.Destination.ToUpperInvariant();

            var sourceExists = await _context.Airports.AnyAsync(a => a.Code == source && a.IsActive);
            if (!sourceExists) throw new NotFoundException("Airport", route.Source);

            var destExists = await _context.Airports.AnyAsync(a => a.Code == destination && a.IsActive);
            if (!destExists) throw new NotFoundException("Airport", route.Destination);

            var existing = await _context.Routes.FirstOrDefaultAsync(r => r.Source == source && r.Destination == destination);
            if (existing != null)
            {
                if (existing.IsActive)
                    throw new ConflictException("Route already exists.");
                else
                {
                    existing.IsActive = true;
                    existing.DistanceKm = route.DistanceKm;
                    existing.EstimatedDuration = route.EstimatedDuration;
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Route reactivated.", route = existing });
                }
            }

            route.Source = source;
            route.Destination = destination;
            route.CreatedAt = DateTime.UtcNow;

            _context.Routes.Add(route);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Route added.", route });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("routes/{routeId:int}")]
        public async Task<IActionResult> UpdateRoute(int routeId, [FromBody] Flight.Domain.Entities.Route updated)
        {
            var route = await _context.Routes.FindAsync(routeId);
            if (route == null) throw new NotFoundException("Route", routeId);

            if (!string.IsNullOrWhiteSpace(updated.Source)) 
            {
                var source = updated.Source.ToUpperInvariant();
                var exists = await _context.Airports.AnyAsync(a => a.Code == source && a.IsActive);
                if (!exists) throw new NotFoundException("Airport", updated.Source);
                route.Source = source;
            }
            if (!string.IsNullOrWhiteSpace(updated.Destination)) 
            {
                var dest = updated.Destination.ToUpperInvariant();
                var exists = await _context.Airports.AnyAsync(a => a.Code == dest && a.IsActive);
                if (!exists) throw new NotFoundException("Airport", updated.Destination);
                route.Destination = dest;
            }
            if (updated.DistanceKm > 0) route.DistanceKm = updated.DistanceKm;
            if (!string.IsNullOrWhiteSpace(updated.EstimatedDuration)) route.EstimatedDuration = updated.EstimatedDuration;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Route updated.", route });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("routes/{routeId:int}")]
        public async Task<IActionResult> DeleteRoute(int routeId)
        {
            var route = await _context.Routes.FindAsync(routeId);
            if (route == null) throw new NotFoundException("Route", routeId);

            route.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Route deactivated.", routeId });
        }

        // ─── Airports (DB-backed) ───────────────────────────────────

        [AllowAnonymous]
        [HttpGet("airports")]
        public async Task<IActionResult> GetAirports()
        {
            var airports = await _context.Airports
                .Where(a => a.IsActive)
                .OrderBy(a => a.Code)
                .ToListAsync();
            return Ok(airports);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("airports")]
        public async Task<IActionResult> AddAirport([FromBody] Airport airport)
        {
            if (string.IsNullOrWhiteSpace(airport.Code) || string.IsNullOrWhiteSpace(airport.Name))
                throw new BadRequestException("Airport code and name are required.");

            var codeUpper = airport.Code.ToUpperInvariant();
            var existing = await _context.Airports.FirstOrDefaultAsync(a => a.Code == codeUpper);
            if (existing != null)
            {
                if (existing.IsActive)
                    throw new ConflictException($"Airport with code '{airport.Code}' already exists.");
                else
                {
                    existing.IsActive = true;
                    existing.Name = airport.Name;
                    existing.City = airport.City;
                    existing.Country = airport.Country;
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Airport reactivated.", airport = existing });
                }
            }

            airport.Code = codeUpper;
            airport.CreatedAt = DateTime.UtcNow;

            _context.Airports.Add(airport);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Airport added.", airport });
        }

        [HttpGet("airports/{code}")]
        public async Task<IActionResult> GetAirportByCode(string code)
        {
            var airport = await _context.Airports.FirstOrDefaultAsync(a => a.Code == code.ToUpperInvariant() && a.IsActive);
            if (airport == null) throw new NotFoundException("Airport", code);
            return Ok(airport);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("airports/{code}")]
        public async Task<IActionResult> UpdateAirport(string code, [FromBody] Airport updated)
        {
            var airport = await _context.Airports.FirstOrDefaultAsync(a => a.Code == code.ToUpperInvariant());
            if (airport == null) throw new NotFoundException("Airport", code);

            if (!string.IsNullOrWhiteSpace(updated.Name)) airport.Name = updated.Name;
            if (!string.IsNullOrWhiteSpace(updated.City)) airport.City = updated.City;
            if (!string.IsNullOrWhiteSpace(updated.Country)) airport.Country = updated.Country;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Airport updated.", airport });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("airports/{code}")]
        public async Task<IActionResult> DeleteAirport(string code)
        {
            var airport = await _context.Airports.FirstOrDefaultAsync(a => a.Code == code.ToUpperInvariant());
            if (airport == null) throw new NotFoundException("Airport", code);

            airport.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Airport deactivated.", code });
        }
    }

    // ─── DTO for Update ─────────────────────────────────────────────
    public class UpdateFlightDto
    {
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureTime { get; set; }
        public DateTime? ArrivalTime { get; set; }
        public string? GateNumber { get; set; }
        public string? Status { get; set; }
        public int? TotalCapacity { get; set; }
        public int? EconomySeats { get; set; }
        public decimal? EconomyPrice { get; set; }
        public int? BusinessSeats { get; set; }
        public decimal? BusinessPrice { get; set; }
        public int? FirstClassSeats { get; set; }
        public decimal? FirstClassPrice { get; set; }
        public decimal? WindowSeatCharge { get; set; }
    }
}
