using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Flight.Application.CQRS.Queries;
using Flight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flight.API.CQRS.Handlers
{
    public class GetAllFlightsQueryHandler : IRequestHandler<GetAllFlightsQuery, List<object>>
    {
        private readonly FlightDbContext _context;
        public GetAllFlightsQueryHandler(FlightDbContext context) { _context = context; }

        public async Task<List<object>> Handle(GetAllFlightsQuery request, CancellationToken cancellationToken)
        {
            var flights = await _context.Flights
                .Where(f => f.Status != "Deleted")
                .ToListAsync(cancellationToken);

            var flightIds = flights.Select(f => f.FlightId).ToList();
            var pricings = await _context.FlightPricings
                .Where(p => flightIds.Contains(p.FlightId) && p.IsActive)
                .ToListAsync(cancellationToken);

            var seatConfigs = await _context.SeatConfigurations
                .Where(s => flightIds.Contains(s.FlightId))
                .ToListAsync(cancellationToken);

            return flights.Select(flight =>
            {
                var rulesForFlight = pricings.Where(r => r.FlightId == flight.FlightId).ToList();
                decimal startingPrice = rulesForFlight.Any()
                    ? rulesForFlight.Min(r => r.BasePrice * r.Multiplier)
                    : 0m;

                var seatsForFlight = seatConfigs.Where(s => s.FlightId == flight.FlightId).ToList();

                var ecoPricing = rulesForFlight.FirstOrDefault(r => r.Class == "Economy");
                var busPricing = rulesForFlight.FirstOrDefault(r => r.Class == "Business");
                var firstPricing = rulesForFlight.FirstOrDefault(r => r.Class != null && r.Class.StartsWith("First"));

                var ecoSeats = seatsForFlight.FirstOrDefault(s => s.Class == "Economy")?.TotalCapacity ?? 0;
                var busSeats = seatsForFlight.FirstOrDefault(s => s.Class == "Business")?.TotalCapacity ?? 0;
                var firstSeats = seatsForFlight.FirstOrDefault(s => s.Class != null && s.Class.StartsWith("First"))?.TotalCapacity ?? 0;

                return (object)new
                {
                    flight.FlightId,
                    flight.FlightNumber,
                    flight.Source,
                    flight.Destination,
                    flight.DepartureTime,
                    flight.ArrivalTime,
                    flight.SourceTimeZone,
                    flight.DestinationTimeZone,
                    flight.AircraftId,
                    Status = flight.DepartureTime < DateTime.UtcNow ? "Cancelled" : flight.Status,
                    flight.AvailableSeats,
                    flight.GateNumber,
                    price = startingPrice,
                    economyPrice = ecoPricing != null ? ecoPricing.BasePrice * ecoPricing.Multiplier : 0,
                    businessPrice = busPricing != null ? busPricing.BasePrice * busPricing.Multiplier : 0,
                    firstClassPrice = firstPricing != null ? firstPricing.BasePrice * firstPricing.Multiplier : 0,
                    windowSeatPrice = ecoPricing?.WindowSeatCharge ?? 0,
                    economySeats = ecoSeats,
                    businessSeats = busSeats,
                    firstClassSeats = firstSeats,
                    totalSeats = flight.AvailableSeats
                };
            }).ToList();
        }
    }
}
