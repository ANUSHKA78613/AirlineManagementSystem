using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using Shared.Middleware.Exceptions;
using Serilog;

namespace Identity.Application.CQRS.Handlers
{
    public class VerifyRegistrationOtpCommandHandler : IRequestHandler<VerifyRegistrationOtpCommand, bool>
    {
        private readonly IIdentityDbContext _context;

        public VerifyRegistrationOtpCommandHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(VerifyRegistrationOtpCommand request, CancellationToken cancellationToken)
        {
            var verification = await _context.FindRegistrationVerificationByEmailAsync(request.Email, cancellationToken);

            if (verification == null)
            {
                throw new BadRequestException("No verification code was sent to this email.");
            }

            if (verification.ExpiryTime < DateTime.UtcNow)
            {
                throw new BadRequestException("Verification code has expired. Please request a new one.");
            }

            if (verification.OtpCode != request.OtpCode)
            {
                throw new BadRequestException("Invalid verification code.");
            }

            verification.IsVerified = true;
            await _context.SaveChangesAsync(cancellationToken);

            Log.Information("Email {Email} successfully verified for registration.", request.Email);
            return true;
        }
    }
}
