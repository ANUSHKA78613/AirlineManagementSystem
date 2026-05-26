using Operations.Domain.Entities;

namespace Operations.Application.Interfaces
{
    public interface IEmployeeRepository
    {
        Task<Employee?> GetByIdAsync(int id);
        Task<IReadOnlyList<Employee>> GetAllAsync();
        Task AddAsync(Employee employee);
        Task SaveChangesAsync();
    }

    public interface IBoardingPassRepository
    {
        Task<BoardingPass?> GetByPnrAndPassengerAsync(string pnr, int passengerId);
        Task AddAsync(BoardingPass boardingPass);
        Task SaveChangesAsync();
    }

    public interface IBaggageRepository
    {
        Task<Baggage?> GetByIdAsync(int id);
        Task<IReadOnlyList<Baggage>> GetByPnrAsync(string pnr);
        Task<IReadOnlyList<Baggage>> GetAllAsync();
        Task<bool> TagExistsAsync(string tagNumber);
        Task AddAsync(Baggage baggage);
        Task SaveChangesAsync();
    }

    public interface IIssueRepository
    {
        Task<Issue?> GetByIdAsync(int id);
        Task<IReadOnlyList<Issue>> GetAllAsync(string? status = null);
        Task AddAsync(Issue issue);
        Task SaveChangesAsync();
    }

}
