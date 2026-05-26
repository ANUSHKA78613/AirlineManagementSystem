using MediatR;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Identity.Application.CQRS.Queries;
using Identity.Application.Interfaces;
using System;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Middleware.Exceptions;

namespace Identity.Application.CQRS.Handlers
{
    public class LoginUserQueryHandler : IRequestHandler<LoginUserQuery, string>
    {
        private readonly IIdentityDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LoginUserQueryHandler> _logger;

        // RFC 5322 compliant email regex
        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public LoginUserQueryHandler(
            IIdentityDbContext context,
            IJwtTokenService jwtTokenService,
            IHttpClientFactory httpClientFactory,
            ILogger<LoginUserQueryHandler> logger)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> Handle(LoginUserQuery request, CancellationToken cancellationToken)
        {
            var dto = request.LoginDto;

            // Email validation
            if (string.IsNullOrWhiteSpace(dto.Email) || !EmailRegex.IsMatch(dto.Email))
            {
                throw new BadRequestException("Please provide a valid email address (e.g. user@example.com).");
            }

            var user = await _context.FindUserByEmailAsync(dto.Email, cancellationToken);
            if (user == null)
            {
                Console.WriteLine($"[LOGIN_DEBUG] User not found for email: {dto.Email}");
                throw new UnauthorizedException("Invalid email or password.");
            }
            Console.WriteLine($"[LOGIN_DEBUG] User found: {user.Email}, Role: {user.Role}, IsVerified: {user.IsVerified}");

            if (!user.IsVerified)
            {
                throw new UnauthorizedException("Your email address has not been verified. Please verify your email before logging in.");
            }

            // Edge Case: Check Lockout
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var remainingMinutes = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                throw new DomainException($"Account is locked out. Try again in {remainingMinutes} minutes.");
            }

            bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            Console.WriteLine($"[LOGIN_DEBUG] Password verification for {user.Email}: {isPasswordCorrect}");

            if (!isPasswordCorrect)
            {
                user.FailedLoginAttempts++;
                Console.WriteLine($"[LOGIN_DEBUG] Failed attempt {user.FailedLoginAttempts} for {user.Email}");
                if (user.FailedLoginAttempts >= 3)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                }
                await _context.SaveChangesAsync(cancellationToken);

                if (user.LockoutEnd.HasValue)
                    throw new DomainException("Account is locked out due to multiple failed attempts. Try again in 15 minutes.");
                else
                    throw new UnauthorizedException($"Invalid email or password. Attempt {user.FailedLoginAttempts} of 3.");
            }

            // Success: Reset metrics
            if (user.FailedLoginAttempts > 0 || user.LockoutEnd.HasValue)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
                await _context.SaveChangesAsync(cancellationToken);
            }

            // ── Dealer Active Status Check ──
            if (string.Equals(user.Role, "Dealer", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var httpClient = _httpClientFactory.CreateClient("DealerService");
                    var response = await httpClient.GetAsync(
                        $"api/Dealer/internal/check-active?email={Uri.EscapeDataString(user.Email)}",
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        var result = JsonSerializer.Deserialize<JsonElement>(json);

                        var found = result.TryGetProperty("found", out var foundProp) && foundProp.GetBoolean();
                        var isActive = result.TryGetProperty("isActive", out var activeProp) && activeProp.GetBoolean();

                        if (found && !isActive)
                        {
                            _logger.LogWarning("Login denied for deactivated dealer: {Email}", user.Email);
                            throw new ForbiddenException("Your dealer account has been deactivated by the administrator. Please contact support.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not verify dealer status — Dealer service returned {StatusCode}", response.StatusCode);
                    }
                }
                catch (ForbiddenException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify dealer active status for {Email}. Allowing login.", user.Email);
                }
            }

            return _jwtTokenService.GenerateToken(user);
        }
    }
}
