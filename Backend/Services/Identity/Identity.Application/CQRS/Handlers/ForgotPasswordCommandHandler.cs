using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using System;
using Serilog;

namespace Identity.Application.CQRS.Handlers
{
    public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, string>
    {
        private readonly IIdentityDbContext _context;
        private readonly Identity.Application.Interfaces.IOtpService _otpService;

        public ForgotPasswordCommandHandler(IIdentityDbContext context, Identity.Application.Interfaces.IOtpService otpService)
        {
            _context = context;
            _otpService = otpService;
        }

        public async Task<string> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.FindUserByEmailAsync(request.Dto.Email, cancellationToken);
            if (user == null)
            {
                return "If the email is registered, an OTP will be sent.";
            }

            await _otpService.GenerateAndSendOtpAsync(user.Email, "password-reset");

            // Fetch the generated OTP from the database
            user = await _context.FindUserByEmailAsync(request.Dto.Email, cancellationToken);
            var otp = user.OtpCode;
            
            Log.Information("Password reset OTP for {Email}: {Otp}", user.Email, otp);
            return $"OTP has been sent. Your verification code is: {otp}";
        }
    }
}

