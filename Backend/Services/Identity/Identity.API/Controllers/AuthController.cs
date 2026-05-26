using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Identity.Application.DTOs;
using Identity.Application.CQRS.Commands;
using Identity.Application.CQRS.Queries;
using System;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.ImageService;
using Identity.Application.Interfaces;
using Shared.Middleware.Exceptions;

namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IdentityDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IImageService _imageService;
        private readonly IRefreshTokenService _refreshTokenService;

        public AuthController(IMediator mediator, IdentityDbContext context, IJwtTokenService jwtTokenService, IImageService imageService, IRefreshTokenService refreshTokenService)
        {
            _mediator = mediator;
            _context = context;
            _jwtTokenService = jwtTokenService;
            _imageService = imageService;
            _refreshTokenService = refreshTokenService;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!string.Equals(dto.Role, "Passenger", StringComparison.OrdinalIgnoreCase) && 
                !string.Equals(dto.Role, "Dealer", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Only Passenger and Dealer roles can be registered.");
            }

            var result = await _mediator.Send(new RegisterUserCommand(dto));
            return Ok(new { message = result });
        }

        [AllowAnonymous]
        [HttpPost("registration-otp/send")]
        public async Task<IActionResult> SendRegistrationOtp([FromBody] RegistrationOtpDto dto)
        {
            var result = await _mediator.Send(new SendRegistrationOtpCommand(dto.Email));
            return Ok(new { message = result });
        }

        [AllowAnonymous]
        [HttpPost("registration-otp/verify")]
        public async Task<IActionResult> VerifyRegistrationOtp([FromBody] VerifyRegistrationOtpDto dto)
        {
            var result = await _mediator.Send(new VerifyRegistrationOtpCommand(dto.Email, dto.OtpCode));
            if (result)
            {
                return Ok(new { message = "Email verified successfully." });
            }
            throw new BadRequestException("Invalid or expired verification code.");
        }

        // Register the first admin user. Only works when no admin exists.
        [AllowAnonymous]
        [HttpPost("register-admin")]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterDto dto)
        {
            var adminExists = await _context.Users.AnyAsync(u => u.Role == "Admin");
            if (adminExists)
                throw new ConflictException("An admin user already exists. Use normal login.");

            dto.Role = "Admin";
            await _mediator.Send(new RegisterUserCommand(dto));
            return Ok(new { message = "Admin registered successfully. You can now log in." });
        }

        // Check if an admin user exists in the system.
        [AllowAnonymous]
        [HttpGet("admin-exists")]
        public async Task<IActionResult> AdminExists()
        {
            var exists = await _context.Users.AnyAsync(u => u.Role == "Admin");
            return Ok(new { adminExists = exists });
        }

        // Emergency: Unlock admin account and reset password if locked out.
        [AllowAnonymous]
        [HttpPost("reset-admin-lockout")]
        public async Task<IActionResult> ResetAdminLockout([FromBody] ResetAdminLockoutDto dto)
        {
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.Role == "Admin");
            if (admin == null)
                throw new NotFoundException("Admin account", dto.Email);

            admin.LockoutEnd = null;
            admin.FailedLoginAttempts = 0;

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Admin account unlocked successfully. You can now log in." });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var token = await _mediator.Send(new LoginUserQuery(dto));
            
            // Generate refresh token and try to store in Redis (non-blocking)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();
            if (user != null)
            {
                try
                {
                    await _refreshTokenService.StoreRefreshTokenAsync(user.UserId, refreshToken);
                }
                catch (Exception)
                {
                    // Redis failure should not block login - continue with empty refresh token
                    refreshToken = "";
                }
            }

            return Ok(new { token, refreshToken, user = user != null ? new { user.UserId, user.Name, user.Email, user.Role, user.Phone } : null });
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var result = await _mediator.Send(new ForgotPasswordCommand(dto));
            return Ok(new { message = result });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var result = await _mediator.Send(new ResetPasswordCommand(dto));
            return Ok(new { message = result });
        }

        [AllowAnonymous]
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpDto dto)
        {
            if (string.IsNullOrEmpty(dto.EmailOrPhone))
                throw new BadRequestException("Email or phone number is required.");

            var result = await _mediator.Send(new SendOtpCommand(dto));
            return Ok(new { message = result });
        }

        [AllowAnonymous]
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            if (string.IsNullOrEmpty(dto.EmailOrPhone) || string.IsNullOrEmpty(dto.OtpCode))
                throw new BadRequestException("Email/phone and OTP code are required.");

            var result = await _mediator.Send(new VerifyOtpCommand(dto));
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.EmailOrPhone || u.Phone == dto.EmailOrPhone);
            var token = result && user != null ? _jwtTokenService.GenerateToken(user) : null;
            return Ok(new { message = "OTP verified successfully", verified = result, token });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? role = null)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(user => user.Role == role);
            }

            var users = await query
                .OrderByDescending(user => user.CreatedAt)
                .Select(user => new
                {
                    userId = user.UserId,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    createdAt = user.CreatedAt,
                    failedLoginAttempts = user.FailedLoginAttempts,
                    lockedUntil = user.LockoutEnd
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("users/{userId:int}/block")]
        public async Task<IActionResult> BlockUser(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new NotFoundException("User", userId);

            user.LockoutEnd = DateTime.UtcNow.AddYears(5);
            await _context.SaveChangesAsync();
            return Ok(new { message = "User blocked.", userId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("users/{userId:int}/unblock")]
        public async Task<IActionResult> UnblockUser(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new NotFoundException("User", userId);

            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;
            await _context.SaveChangesAsync();
            return Ok(new { message = "User unblocked.", userId });
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedException("Invalid token.");

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new NotFoundException("User", userId);

            var profileImage = user.ProfileImageBytes != null && user.ProfileImageBytes.Length > 0
                ? $"data:image/jpeg;base64,{Convert.ToBase64String(user.ProfileImageBytes)}"
                : user.ProfileImageUrl; // Fallback to SSO provider image

            return Ok(new
            {
                userId = user.UserId,
                name = user.Name,
                email = user.Email,
                phone = user.Phone,
                role = user.Role,
                createdAt = user.CreatedAt,
                profileImageUrl = profileImage,
                ssoProvider = user.ExternalProvider
            });
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedException("Invalid token.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new NotFoundException("User", userId);

            if (!string.IsNullOrWhiteSpace(dto.Name)) user.Name = dto.Name;
            if (!string.IsNullOrWhiteSpace(dto.Phone)) user.Phone = dto.Phone;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Profile updated.", userId });
        }

        // ─── Passenger Profile Management ────────
        // Note: SavedPassenger endpoints moved to Booking/Passenger Service
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedException("Invalid token.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new NotFoundException("User", userId);

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                throw new BadRequestException("Current password is incorrect.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Password changed successfully." });
        }

        // Upload a profile image (max 2MB, jpg/png/webp)
        [Authorize]
        [HttpPost("profile/image")]
        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedException("Invalid token.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new NotFoundException("User", userId);

            if (file == null || file.Length == 0)
                throw new BadRequestException("File is empty.");

            if (file.Length > 2 * 1024 * 1024)
                throw new BadRequestException("Limit: Profile image must be less than 2MB.");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            user.ProfileImageBytes = memoryStream.ToArray();
            await _context.SaveChangesAsync();

            var base64Url = $"data:image/jpeg;base64,{Convert.ToBase64String(user.ProfileImageBytes)}";
            return Ok(new { message = "Profile image uploaded to database payload natively.", imageUrl = base64Url });
        }

        // Delete profile image
        [Authorize]
        [HttpDelete("profile/image")]
        public async Task<IActionResult> DeleteProfileImage()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedException("Invalid token.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new NotFoundException("User", userId);

            if (user.ProfileImageBytes != null)
            {
                user.ProfileImageBytes = null;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Profile image removed." });
        }

        // ── Refresh Token Endpoint ────────────────────────────────
        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken) || request.UserId <= 0)
                throw new BadRequestException("UserId and RefreshToken are required.");

            var isValid = await _refreshTokenService.ValidateRefreshTokenAsync(request.UserId, request.RefreshToken);
            if (!isValid)
                throw new UnauthorizedException("Invalid or expired refresh token. Please login again.");

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
                throw new NotFoundException("User", request.UserId);

            // Rotate: revoke old, issue new pair
            await _refreshTokenService.RevokeRefreshTokenAsync(user.UserId);

            var newAccessToken = _jwtTokenService.GenerateToken(user);
            var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
            await _refreshTokenService.StoreRefreshTokenAsync(user.UserId, newRefreshToken);

            return Ok(new { token = newAccessToken, refreshToken = newRefreshToken });
        }

        // ── Logout Endpoint ───────────────────────────────────────
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (request.UserId > 0)
            {
                await _refreshTokenService.RevokeRefreshTokenAsync(request.UserId);
            }
            return Ok(new { message = "Logged out. Refresh token revoked." });
        }

        // ── Admin: Database Cleanup ───────────────────────────────
        [Authorize(Roles = "Admin")]
        [HttpDelete("admin/cleanup-database")]
        public async Task<IActionResult> CleanupDatabase()
        {
            // Clean up legacy SavedPassengers table via Raw SQL 
            try 
            {
                await _context.Database.ExecuteSqlRawAsync("IF OBJECT_ID('SavedPassengers', 'U') IS NOT NULL DELETE FROM [SavedPassengers];");
            }
            catch (Exception) { /* Table may not exist */ }

            var protectedEmails = new[] { "visheshmadan678@gmail.com", "anushkalodhi356@gmail.com" };
            var usersToDelete = await _context.Users
                .Where(u => !protectedEmails.Contains(u.Email))
                .ToListAsync();

            if (!usersToDelete.Any())
                return Ok(new { message = "No users to delete. Database is already clean." });

            _context.Users.RemoveRange(usersToDelete);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Cleanup complete. Removed {usersToDelete.Count} users.", deletedCount = usersToDelete.Count });
        }
    }

    public class UpdateProfileDto
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public int UserId { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        public int UserId { get; set; }
    }
}
