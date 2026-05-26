using MediatR;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using BCrypt.Net;
using System;
using Shared.Middleware.Exceptions;

using MassTransit;
using Identity.Application.IntegrationEvents;

namespace Identity.Application.CQRS.Handlers
{
    public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, string>
    {
        private readonly IIdentityDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public RegisterUserCommandHandler(IIdentityDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<string> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            var dto = request.RegisterDto;

            // Email validation
            if (string.IsNullOrWhiteSpace(dto.Email) || !EmailRegex.IsMatch(dto.Email))
            {
                throw new BadRequestException("Please provide a valid email address (e.g. user@example.com).");
            }

            var existingUser = await _context.FindUserByEmailAsync(dto.Email, cancellationToken);
            if (existingUser != null)
            {
                throw new ConflictException("User with this email already exists.");
            }

            // Enforce inline verification check (except for Admin)
            if (dto.Role != "Admin")
            {
                var verification = await _context.FindVerifiedRegistrationByEmailAsync(dto.Email, cancellationToken);
                
                if (verification == null)
                {
                    throw new BadRequestException("Please verify your email address before creating an account.");
                }
                
                // Cleanup verification record
                _context.RemoveRegistrationVerification(verification);
            }

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? string.Empty : dto.Phone,
                Role = dto.Role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                OtpCode = string.Empty,
                OtpExpiryTime = null,
                IsVerified = true // Set to true as they just verified via OTP (or are Admin)
            };

            _context.AddUser(user);

            await _context.SaveChangesAsync(cancellationToken);

            // Publish Integration Event
            var integrationEvent = new UserRegisteredIntegrationEvent(
                user.UserId,
                user.Name,
                user.Email,
                user.Role
            );

            await _publishEndpoint.Publish(integrationEvent, cancellationToken);

            return "User registered successfully. You can now log in.";
        }
    }
}
