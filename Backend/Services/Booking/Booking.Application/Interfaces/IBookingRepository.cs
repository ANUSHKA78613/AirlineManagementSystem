using Booking.Domain.Entities;

namespace Booking.Application.Interfaces
{
    public interface IBookingRepository
    {
        Task<Booking.Domain.Entities.Booking?> GetByPnrAsync(string pnr);
        Task<IReadOnlyList<Booking.Domain.Entities.Booking>> GetByUserIdAsync(int userId);
        Task<IReadOnlyList<Booking.Domain.Entities.Booking>> GetAllAsync();
        Task<bool> HasDuplicateAsync(int userId, int flightId);
        Task AddAsync(Booking.Domain.Entities.Booking booking);
        Task UpdateAsync(Booking.Domain.Entities.Booking booking);
        Task SaveChangesAsync();
    }

    public interface IPassengerRepository
    {
        Task<IReadOnlyList<Passenger>> GetByPnrAsync(string pnr);
        Task AddRangeAsync(IEnumerable<Passenger> passengers);
        Task SaveChangesAsync();
    }
}
