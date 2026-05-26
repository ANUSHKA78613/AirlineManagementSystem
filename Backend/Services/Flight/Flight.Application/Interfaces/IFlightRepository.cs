namespace Flight.Application.Interfaces
{
    public interface IFlightRepository
    {
        Task<Domain.Entities.Flight?> GetByIdAsync(int flightId);
        Task<IReadOnlyList<Domain.Entities.Flight>> GetAllAsync();
        Task<IReadOnlyList<Domain.Entities.Flight>> SearchAsync(string? source, string? destination, DateTime? date);
        Task<bool> ExistsByFlightNumberAsync(string flightNumber);
        Task AddAsync(Domain.Entities.Flight flight);
        Task UpdateAsync(Domain.Entities.Flight flight);
        Task SaveChangesAsync();
    }
}
