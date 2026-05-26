using System;
using System.Security.Cryptography;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Middleware.Exceptions;
using Identity.Application.Interfaces;
using Serilog;

namespace Identity.Infrastructure.Services
{
    public class OtpService : IOtpService
    {
        private readonly IdentityDbContext _dbContext;
        private readonly Shared.EmailService.IEmailService _emailService;
        private const int OtpLength = 6;
        private const int OtpExpiryMinutes = 1;

        public OtpService(IdentityDbContext dbContext, Shared.EmailService.IEmailService emailService)
        {
            _dbContext = dbContext;
            _emailService = emailService;
        }

        public async Task<string> GenerateAndSendOtpAsync(string email, string type)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new NotFoundException("User", email);

            if (user.OtpExpiryTime.HasValue && user.OtpExpiryTime.Value > DateTime.UtcNow)
            {
                var lastGeneratedAt = user.OtpExpiryTime.Value.AddMinutes(-OtpExpiryMinutes);
                var secondsSinceLastOtp = (DateTime.UtcNow - lastGeneratedAt).TotalSeconds;
                if (secondsSinceLastOtp < 60)
                {
                    var waitTime = Math.Ceiling(60 - secondsSinceLastOtp);
                    throw new DomainException($"Please wait {waitTime} seconds before requesting a new OTP.");
                }
            }

            string otpCode = GenerateOtpCode();
            var expiryTime = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);

            user.OtpCode = otpCode;
            user.OtpExpiryTime = expiryTime;
            await _dbContext.SaveChangesAsync();

            // Fire and forget email sending
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendOtpEmailAsync(email, user.Name ?? "User", otpCode, OtpExpiryMinutes);
                    Log.Information("OTP sent to {Email} via background task.", email);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Background OTP email failure for {Email}", email);
                }
            });

            Log.Information("OTP generated for user {Email} - background send initiated", user.Email);
            return "OTP sent successfully";
        }

        public async Task<bool> VerifyOtpAsync(string email, string otpCode)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new NotFoundException("User", email);

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                throw new DomainException($"Account is locked out. Try again in {remainingMinutes} minutes.");
            }

            if (!user.OtpExpiryTime.HasValue || string.IsNullOrEmpty(user.OtpCode))
                throw new DomainException("No OTP has been generated. Please request a new OTP first.");

            if (user.OtpExpiryTime.Value < DateTime.UtcNow)
                throw new DomainException("OTP has expired. Please request a new OTP.");

            if (user.OtpCode != otpCode)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 3)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                }
                await _dbContext.SaveChangesAsync();

                if (user.LockoutEnd.HasValue)
                    throw new DomainException("Account is locked out due to multiple failed OTP attempts. Try again in 15 minutes.");
                
                throw new UnauthorizedException($"Invalid OTP code. Attempt {user.FailedLoginAttempts} of 3.");
            }

            user.OtpCode = null;
            user.OtpExpiryTime = null;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _dbContext.SaveChangesAsync();

            Log.Information("OTP verified for user {Email}", user.Email);
            return true;
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
