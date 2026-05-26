namespace Payment.Application.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Domain.Entities.Payment?> GetByIdAsync(int paymentId);
        Task<Domain.Entities.Payment?> GetByTransactionIdAsync(string transactionId);
        Task<IReadOnlyList<Domain.Entities.Payment>> GetByPnrAsync(string pnr);
        Task<IReadOnlyList<Domain.Entities.Payment>> GetAllAsync();
        Task AddAsync(Domain.Entities.Payment payment);
        Task UpdateAsync(Domain.Entities.Payment payment);
        Task SaveChangesAsync();
    }
}
