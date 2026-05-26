using Dealer.Domain.Entities;

namespace Dealer.Application.Interfaces
{
    public interface IDealerRepository
    {
        Task<DealerAgent?> GetByIdAsync(int id);
        Task<DealerAgent?> GetByAgentCodeAsync(string agentCode);
        Task<IReadOnlyList<DealerAgent>> GetAllAsync(bool? activeOnly = null);
        Task AddAsync(DealerAgent agent);
        Task UpdateAsync(DealerAgent agent);
        Task SaveChangesAsync();
    }

    public interface IWalletRepository
    {
        Task<DealerWallet?> GetByAgentIdAsync(int agentId);
        Task AddAsync(DealerWallet wallet);
        Task AddTransactionAsync(WalletTransaction transaction);
        Task<IReadOnlyList<WalletTransaction>> GetTransactionsAsync(int walletId, int page, int pageSize);
        Task<int> GetTransactionCountAsync(int walletId);
        Task SaveChangesAsync();
    }

    public interface ICommissionRepository
    {
        Task<CommissionRecord?> GetByIdAsync(int id);
        Task<IReadOnlyList<CommissionRecord>> GetByAgentCodeAsync(string agentCode, string? status = null);
        Task AddAsync(CommissionRecord commission);
        Task UpdateAsync(CommissionRecord commission);
        Task SaveChangesAsync();
    }
}
