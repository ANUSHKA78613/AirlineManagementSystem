using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using System;
using Shared.Middleware.Exceptions;

namespace Identity.Application.CQRS.Handlers
{
    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, string>
    {
        private readonly IIdentityDbContext _context;

        public ResetPasswordCommandHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<string> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;
            var user = await _context.FindUserByEmailAsync(dto.Email, cancellationToken);
            
            if (user == null || user.OtpCode != dto.OtpCode)
            {
                throw new UnauthorizedException("Invalid OTP or Email.");
            }

            if (user.OtpExpiryTime.HasValue && user.OtpExpiryTime.Value < DateTime.UtcNow)
            {
                throw new DomainException("OTP has expired.");
            }

            // Reset password logic
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.OtpCode = null;
            user.OtpExpiryTime = null;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            await _context.SaveChangesAsync(cancellationToken);

            return "Password has been successfully reset.";
        }
    }
}
