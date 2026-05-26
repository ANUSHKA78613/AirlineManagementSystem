using Flight.Application.Interfaces;
using Flight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flight.Infrastructure.Repositories
{
    public class FlightRepository : IFlightRepository
    {
        private readonly FlightDbContext _context;
        public FlightRepository(FlightDbContext context) { _context = context; }

        public async Task<Domain.Entities.Flight?> GetByIdAsync(int flightId)
            => await _context.Flights.FirstOrDefaultAsync(f => f.FlightId == flightId);

        public async Task<IReadOnlyList<Domain.Entities.Flight>> GetAllAsync()
            => await _context.Flights.AsNoTracking().ToListAsync();

        public async Task<IReadOnlyList<Domain.Entities.Flight>> SearchAsync(string? source, string? destination, DateTime? date)
        {
            var query = _context.Flights.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(source)) query = query.Where(f => f.Source == source);
            if (!string.IsNullOrWhiteSpace(destination)) query = query.Where(f => f.Destination == destination);
            if (date.HasValue) query = query.Where(f => f.DepartureTime.Date == date.Value.Date);
            return await query.ToListAsync();
        }

        public async Task<bool> ExistsByFlightNumberAsync(string flightNumber)
            => await _context.Flights.AnyAsync(f => f.FlightNumber == flightNumber);

        public async Task AddAsync(Domain.Entities.Flight flight)
            => await _context.Flights.AddAsync(flight);

        public Task UpdateAsync(Domain.Entities.Flight flight)
        {
            _context.Flights.Update(flight);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
