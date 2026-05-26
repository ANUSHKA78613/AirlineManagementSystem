using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Shared.Middleware.Exceptions;
using System.Security.Cryptography;
using Serilog;

namespace Identity.Application.CQRS.Handlers
{
    public class SendRegistrationOtpCommandHandler : IRequestHandler<SendRegistrationOtpCommand, string>
    {
        private readonly IIdentityDbContext _context;
        private readonly IEmailService _emailService;

        public SendRegistrationOtpCommandHandler(IIdentityDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<string> Handle(SendRegistrationOtpCommand request, CancellationToken cancellationToken)
        {
            // 1. Check if user already exists
            var existingUser = await _context.UserExistsByEmailAsync(request.Email, cancellationToken);
            if (existingUser)
            {
                throw new ConflictException("A user with this email already exists.");
            }

            // 2. Generate OTP
            string otpCode = GenerateOtpCode();
            var expiryTime = DateTime.UtcNow.AddMinutes(10);

            // 3. Upsert into RegistrationVerifications
            var verification = await _context.FindRegistrationVerificationByEmailAsync(request.Email, cancellationToken);

            if (verification == null)
            {
                verification = new RegistrationVerification
                {
                    Email = request.Email,
                    OtpCode = otpCode,
                    ExpiryTime = expiryTime,
                    IsVerified = false
                };
                _context.AddRegistrationVerification(verification);
            }
            else
            {
                verification.OtpCode = otpCode;
                verification.ExpiryTime = expiryTime;
                verification.IsVerified = false; // Reset if they are re-sending
            }

            await _context.SaveChangesAsync(cancellationToken);

            // 4. Send Email (Non-blocking background task)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendOtpEmailAsync(request.Email, "Valued Traveler", otpCode, 10);
                    Log.Information("Registration OTP {Otp} sent to {Email} asynchronously.", otpCode, request.Email);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to send registration OTP (background) to {Email}", request.Email);
                }
            }, cancellationToken);

            return "Verification code sent successfully.";
        }

        private string GenerateOtpCode()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] data = new byte[4];
            rng.GetBytes(data);
            int number = BitConverter.ToInt32(data, 0) & 0x7FFFFFFF;
            return (number % 1000000).ToString("D6");
        }
    }
}
