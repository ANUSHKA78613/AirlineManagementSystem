using Booking.Application.Interfaces;
using Booking.Domain.Entities;
using Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Booking.Infrastructure.Repositories
{
    public class BookingRepository : IBookingRepository
    {
        private readonly BookingDbContext _context;
        public BookingRepository(BookingDbContext context) { _context = context; }

        public async Task<Booking.Domain.Entities.Booking?> GetByPnrAsync(string pnr) => await _context.Bookings.FirstOrDefaultAsync(b => b.PNR == pnr);
        public async Task<IReadOnlyList<Booking.Domain.Entities.Booking>> GetByUserIdAsync(int userId)
            => await _context.Bookings.AsNoTracking().Where(b => b.UserId == userId).OrderByDescending(b => b.BookingDate).ToListAsync();
        public async Task<IReadOnlyList<Booking.Domain.Entities.Booking>> GetAllAsync()
            => await _context.Bookings.AsNoTracking().OrderByDescending(b => b.BookingDate).ToListAsync();
        public async Task<bool> HasDuplicateAsync(int userId, int flightId)
            => await _context.Bookings.AnyAsync(b => b.UserId == userId && b.FlightId == flightId && b.Status != "Cancelled");
        public async Task AddAsync(Booking.Domain.Entities.Booking booking) => await _context.Bookings.AddAsync(booking);
        public Task UpdateAsync(Booking.Domain.Entities.Booking booking) { _context.Bookings.Update(booking); return Task.CompletedTask; }
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }

    public class PassengerRepository : IPassengerRepository
    {
        private readonly BookingDbContext _context;
        public PassengerRepository(BookingDbContext context) { _context = context; }

        public async Task<IReadOnlyList<Passenger>> GetByPnrAsync(string pnr)
            => await _context.Passengers.AsNoTracking().Where(p => p.PNR == pnr).OrderBy(p => p.PassengerId).ToListAsync();
        public async Task AddRangeAsync(IEnumerable<Passenger> passengers)
            => await _context.Passengers.AddRangeAsync(passengers);
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
