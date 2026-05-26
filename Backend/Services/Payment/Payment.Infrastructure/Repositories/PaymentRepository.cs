using Payment.Application.Interfaces;
using Payment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Payment.Infrastructure.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly PaymentDbContext _context;
        public PaymentRepository(PaymentDbContext context) { _context = context; }

        public async Task<Domain.Entities.Payment?> GetByIdAsync(int paymentId)
            => await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        public async Task<Domain.Entities.Payment?> GetByTransactionIdAsync(string transactionId)
            => await _context.Payments.FirstOrDefaultAsync(p => p.TransactionId == transactionId);
        public async Task<IReadOnlyList<Domain.Entities.Payment>> GetByPnrAsync(string pnr)
            => await _context.Payments.AsNoTracking().Where(p => p.PNR == pnr).OrderByDescending(p => p.CreatedAt).ToListAsync();
        public async Task<IReadOnlyList<Domain.Entities.Payment>> GetAllAsync()
            => await _context.Payments.AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync();
        public async Task AddAsync(Domain.Entities.Payment payment) => await _context.Payments.AddAsync(payment);
        public Task UpdateAsync(Domain.Entities.Payment payment) { _context.Payments.Update(payment); return Task.CompletedTask; }
        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
