using MediatR;
using Payment.Application.CQRS.Commands;
using Payment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Middleware.Exceptions;

namespace Payment.API.CQRS.Handlers
{
    public class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, object>
    {
        private readonly PaymentDbContext _context;
        public ProcessPaymentCommandHandler(PaymentDbContext context) { _context = context; }

        public async Task<object> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;

            if (dto.Amount <= 0) throw new BadRequestException("Payment amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(dto.PNR)) throw new BadRequestException("PNR is required.");
            if (string.IsNullOrWhiteSpace(dto.PaymentMethod)) throw new BadRequestException("Payment method is required.");

            var now = DateTime.UtcNow;
            var duplicateWindowStart = now.AddMinutes(-10);
            var failureWindowStart = now.AddMinutes(-15);

            var duplicateSuccess = await _context.Payments.AnyAsync(
                payment =>
                    payment.PNR == dto.PNR &&
                    payment.Amount == dto.Amount &&
                    payment.Status == "Success" &&
                    payment.CreatedAt >= duplicateWindowStart,
                cancellationToken
            );
            if (duplicateSuccess)
                throw new ConflictException("Duplicate payment detected for this PNR. Please verify the booking before retrying.");

            var recentFailures = await _context.Payments.CountAsync(
                payment =>
                    payment.PNR == dto.PNR &&
                    payment.Status == "Failed" &&
                    payment.CreatedAt >= failureWindowStart,
                cancellationToken
            );
            if (recentFailures >= 3)
                throw new DomainException("Payment temporarily blocked due to repeated failed attempts. Please contact support.");

            // Simulate payment processing
            var payment = new Payment.Domain.Entities.Payment
            {
                PNR = dto.PNR,
                Amount = dto.Amount,
                PaymentMethod = dto.PaymentMethod,
                Status = "Success" // Simulated success
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            return new
            {
                paymentId = payment.PaymentId,
                transactionId = payment.TransactionId,
                status = payment.Status,
                message = "Payment processed successfully."
            };
        }
    }
}
