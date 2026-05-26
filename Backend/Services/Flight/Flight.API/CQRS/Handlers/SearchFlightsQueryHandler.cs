using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Flight.Application.CQRS.Queries;
using Flight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flight.API.CQRS.Handlers
{
    public class SearchFlightsQueryHandler : IRequestHandler<SearchFlightsQuery, List<object>>
    {
        private readonly FlightDbContext _context;
        public SearchFlightsQueryHandler(FlightDbContext context) { _context = context; }

        public async Task<List<object>> Handle(SearchFlightsQuery request, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var query = _context.Flights.AsQueryable();

            if (!string.IsNullOrEmpty(request.Dto.Source))
                query = query.Where(f => f.Source == request.Dto.Source);

            if (!string.IsNullOrEmpty(request.Dto.Destination))
                query = query.Where(f => f.Destination == request.Dto.Destination);

            if (request.Dto.DepartureDate.HasValue)
                query = query.Where(f => f.DepartureTime.Date == request.Dto.DepartureDate.Value.Date);

            query = query.Where(f => f.Status == "Scheduled" && f.DepartureTime > DateTime.UtcNow);

            var flights = await query.ToListAsync(cts.Token);

            // Get all pricing rules for these flights
            var flightIds = flights.Select(f => f.FlightId).ToList();
            var pricingRules = await _context.FlightPricings
                .Where(r => flightIds.Contains(r.FlightId) && r.IsActive)
                .ToListAsync(cts.Token);

            // Build result DTOs with computed starting price
            var results = flights.Select(flight =>
            {
                var rulesForFlight = pricingRules.Where(r => r.FlightId == flight.FlightId).ToList();
                var economyRule = rulesForFlight.FirstOrDefault(r => r.Class == "Economy");
                decimal startingPrice = economyRule != null
                    ? economyRule.BasePrice
                    : (rulesForFlight.Any() ? rulesForFlight.Min(r => r.BasePrice * r.Multiplier) : 0m);

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
                    flight.Status,
                    flight.AvailableSeats,
                    flight.GateNumber,
                    price = startingPrice,
                    totalSeats = flight.AvailableSeats
                };
            }).ToList();

            // Apply Post-Pricing Filters
            if (request.Dto.MinPrice.HasValue)
                results = results.Where(f => ((dynamic)f).price >= request.Dto.MinPrice.Value).ToList();
            if (request.Dto.MaxPrice.HasValue)
                results = results.Where(f => ((dynamic)f).price <= request.Dto.MaxPrice.Value).ToList();

            // Apply Sorting
            results = request.Dto.SortBy?.ToUpper() switch
            {
                "PRICEASC" => results.OrderBy(f => ((dynamic)f).price).ToList(),
                "PRICEDESC" => results.OrderByDescending(f => ((dynamic)f).price).ToList(),
                "TIMEASC" => results.OrderBy(f => ((dynamic)f).DepartureTime).ToList(),
                "TIMEDESC" => results.OrderByDescending(f => ((dynamic)f).DepartureTime).ToList(),
                _ => results.OrderBy(f => ((dynamic)f).DepartureTime).ToList()
            };

            return results;
        }
    }
}
