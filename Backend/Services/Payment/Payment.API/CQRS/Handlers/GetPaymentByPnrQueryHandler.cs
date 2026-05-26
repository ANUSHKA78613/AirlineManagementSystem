using MediatR;
using Payment.Application.CQRS.Queries;
using Payment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Payment.API.CQRS.Handlers
{
    public class GetPaymentByPnrQueryHandler : IRequestHandler<GetPaymentByPnrQuery, List<Payment.Domain.Entities.Payment>>
    {
        private readonly PaymentDbContext _context;
        public GetPaymentByPnrQueryHandler(PaymentDbContext context) { _context = context; }

        public async Task<List<Payment.Domain.Entities.Payment>> Handle(GetPaymentByPnrQuery request, CancellationToken cancellationToken)
        {
            return await _context.Payments.Where(p => p.PNR == request.PNR).ToListAsync(cancellationToken);
        }
    }
}
