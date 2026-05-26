using Operations.Application.Interfaces;
using Operations.Domain.Entities;
using Operations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Operations.Infrastructure.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly OperationsDbContext _context;
        public EmployeeRepository(OperationsDbContext context) { _context = context; }
        public async Task<Employee?> GetByIdAsync(int id) => await _context.Employees.FindAsync(id);
        public async Task<IReadOnlyList<Employee>> GetAllAsync() => await _context.Employees.ToListAsync();
        public async Task AddAsync(Employee employee) => await _context.Employees.AddAsync(employee);
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }

    public class BoardingPassRepository : IBoardingPassRepository
    {
        private readonly OperationsDbContext _context;
        public BoardingPassRepository(OperationsDbContext context) { _context = context; }
        public async Task<BoardingPass?> GetByPnrAndPassengerAsync(string pnr, int passengerId)
            => await _context.BoardingPasses.FirstOrDefaultAsync(b => b.PNR == pnr && b.PassengerId == passengerId);
        public async Task AddAsync(BoardingPass boardingPass) => await _context.BoardingPasses.AddAsync(boardingPass);
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }

    public class BaggageRepository : IBaggageRepository
    {
        private readonly OperationsDbContext _context;
        public BaggageRepository(OperationsDbContext context) { _context = context; }
        public async Task<Baggage?> GetByIdAsync(int id) => await _context.BaggageRecords.FindAsync(id);
        public async Task<IReadOnlyList<Baggage>> GetByPnrAsync(string pnr) => await _context.BaggageRecords.Where(b => b.PNR == pnr).ToListAsync();
        public async Task<IReadOnlyList<Baggage>> GetAllAsync() => await _context.BaggageRecords.OrderByDescending(b => b.CreatedAt).ToListAsync();
        public async Task<bool> TagExistsAsync(string tagNumber) => await _context.BaggageRecords.AnyAsync(b => b.TagNumber == tagNumber);
        public async Task AddAsync(Baggage baggage) => await _context.BaggageRecords.AddAsync(baggage);
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }

    public class IssueRepository : IIssueRepository
    {
        private readonly OperationsDbContext _context;
        public IssueRepository(OperationsDbContext context) { _context = context; }
        public async Task<Issue?> GetByIdAsync(int id) => await _context.Issues.FindAsync(id);
        public async Task<IReadOnlyList<Issue>> GetAllAsync(string? status = null)
        {
            var query = _context.Issues.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(i => i.Status == status);
            return await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        }
        public async Task AddAsync(Issue issue) => await _context.Issues.AddAsync(issue);
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }

}
