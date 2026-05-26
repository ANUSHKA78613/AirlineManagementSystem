using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;

namespace Identity.Application.CQRS.Handlers
{
    public class ProcessSsoLoginCommandHandler : IRequestHandler<Commands.ProcessSsoLoginCommand, string>
    {
        private readonly IIdentityDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;

        public ProcessSsoLoginCommandHandler(IIdentityDbContext context, IJwtTokenService jwtTokenService)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
        }

        public async Task<string> Handle(Commands.ProcessSsoLoginCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.FindUserByExternalProviderOrEmailAsync("Google", request.GoogleId, request.Email, cancellationToken);

            if (user == null)
            {
                user = new User
                {
                    Name = request.Name ?? request.Email.Split('@')[0],
                    Email = request.Email,
                    Phone = string.Empty,
                    PasswordHash = string.Empty,
                    Role = "Passenger",
                    ExternalProvider = "Google",
                    ExternalProviderId = request.GoogleId,
                    ProfileImageUrl = request.Picture,
                    OtpCode = string.Empty,
                    OtpExpiryTime = null,
                    IsVerified = true
                };

                _context.AddUser(user);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else if (string.IsNullOrEmpty(user.ExternalProvider))
            {
                user.ExternalProvider = "Google";
                user.ExternalProviderId = request.GoogleId;
                user.ProfileImageUrl = request.Picture;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return _jwtTokenService.GenerateToken(user);
        }
    }
}
