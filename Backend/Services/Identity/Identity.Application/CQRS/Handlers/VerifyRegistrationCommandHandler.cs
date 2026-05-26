using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using Shared.Middleware.Exceptions;
using Serilog;

namespace Identity.Application.CQRS.Handlers
{
    public class VerifyRegistrationCommandHandler : IRequestHandler<VerifyRegistrationCommand, bool>
    {
        private readonly IOtpService _otpService;
        private readonly IIdentityDbContext _context;

        public VerifyRegistrationCommandHandler(IOtpService otpService, IIdentityDbContext context)
        {
            _otpService = otpService;
            _context = context;
        }

        public async Task<bool> Handle(VerifyRegistrationCommand request, CancellationToken cancellationToken)
        {
            // First, use the OTP service to verify the code
            bool isValid = await _otpService.VerifyOtpAsync(request.Dto.EmailOrPhone, request.Dto.OtpCode);

            if (isValid)
            {
                // Find the user and mark as verified
                var user = await _context.FindUserByEmailOrPhoneAsync(request.Dto.EmailOrPhone, cancellationToken);
                
                if (user != null)
                {
                    user.IsVerified = true;
                    await _context.SaveChangesAsync(cancellationToken);
                    Log.Information("User {Email} has been marked as verified.", user.Email);
                }
            }

            return isValid;
        }
    }
}
