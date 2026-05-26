using Dealer.Application.Interfaces;
using Dealer.Domain.Entities;
using Dealer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealer.Infrastructure.Repositories
{
    public class DealerRepository : IDealerRepository
    {
        private readonly DealerDbContext _context;
        public DealerRepository(DealerDbContext context) { _context = context; }
        public async Task<DealerAgent?> GetByIdAsync(int id) => await _context.DealerAgents.FindAsync(id);
        public async Task<DealerAgent?> GetByAgentCodeAsync(string agentCode) => await _context.DealerAgents.FirstOrDefaultAsync(a => a.AgentCode == agentCode);
        public async Task<IReadOnlyList<DealerAgent>> GetAllAsync(bool? activeOnly = null)
        {
            var query = _context.DealerAgents.AsQueryable();
            if (activeOnly.HasValue && activeOnly.Value) query = query.Where(a => a.IsActive);
            return await query.OrderBy(a => a.AgencyName).ToListAsync();
        }
        public async Task AddAsync(DealerAgent agent) => await _context.DealerAgents.AddAsync(agent);
        public Task UpdateAsync(DealerAgent agent) { _context.DealerAgents.Update(agent); return Task.CompletedTask; }
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }

    public class WalletRepository : IWalletRepository
    {
        private readonly DealerDbContext _context;
        public WalletRepository(DealerDbContext context) { _context = context; }
        public async Task<DealerWallet?> GetByAgentIdAsync(int agentId) => await _context.Wallets.FirstOrDefaultAsync(w => w.AgentId == agentId);
        public async Task AddAsync(DealerWallet wallet) => await _context.Wallets.AddAsync(wallet);
        public async Task AddTransactionAsync(WalletTransaction transaction) => await _context.WalletTransactions.AddAsync(transaction);
        public async Task<IReadOnlyList<WalletTransaction>> GetTransactionsAsync(int walletId, int page, int pageSize)
            => await _context.WalletTransactions.Where(t => t.WalletId == walletId).OrderByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        public async Task<int> GetTransactionCountAsync(int walletId) => await _context.WalletTransactions.CountAsync(t => t.WalletId == walletId);
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }

    public class CommissionRepository : ICommissionRepository
    {
        private readonly DealerDbContext _context;
        public CommissionRepository(DealerDbContext context) { _context = context; }
        public async Task<CommissionRecord?> GetByIdAsync(int id) => await _context.Commissions.FindAsync(id);
        public async Task<IReadOnlyList<CommissionRecord>> GetByAgentCodeAsync(string agentCode, string? status = null)
        {
            var query = _context.Commissions.Where(c => c.AgentCode == agentCode);
            if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);
            return await query.OrderByDescending(c => c.EarnedDate).ToListAsync();
        }
        public async Task AddAsync(CommissionRecord commission) => await _context.Commissions.AddAsync(commission);
        public Task UpdateAsync(CommissionRecord commission) { _context.Commissions.Update(commission); return Task.CompletedTask; }
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
