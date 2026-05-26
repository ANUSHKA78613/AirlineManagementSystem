using MediatR;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Identity.Application.Interfaces;
using Shared.Middleware.Exceptions;

namespace Identity.Application.CQRS.Handlers
{
    public class AdminCommandsHandler : 
        IRequestHandler<Commands.BlockUserCommand, string>,
        IRequestHandler<Commands.UnblockUserCommand, string>,
        IRequestHandler<Commands.AssignRoleCommand, string>
    {
        private readonly IIdentityDbContext _context;

        public AdminCommandsHandler(IIdentityDbContext context)
        {
            _context = context;
        }

        public async Task<string> Handle(Commands.BlockUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.FindUserByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new NotFoundException("User", request.UserId.ToString());

            if (user.Role == "Admin")
                throw new BadRequestException("You cannot block another Admin.");

            user.LockoutEnd = DateTime.UtcNow.AddYears(100);
            await _context.SaveChangesAsync(cancellationToken);
            return $"User {user.Email} has been permanently blocked.";
        }

        public async Task<string> Handle(Commands.UnblockUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.FindUserByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new NotFoundException("User", request.UserId.ToString());

            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;
            await _context.SaveChangesAsync(cancellationToken);
            return $"User {user.Email} has been unblocked.";
        }

        public async Task<string> Handle(Commands.AssignRoleCommand request, CancellationToken cancellationToken)
        {
            var user = await _context.FindUserByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new NotFoundException("User", request.UserId.ToString());

            var validRoles = new[] { "Passenger", "Admin", "Operations", "Dealer" };
            if (!validRoles.Contains(request.NewRole))
                throw new BadRequestException("Invalid role specified.");

            user.Role = request.NewRole;
            await _context.SaveChangesAsync(cancellationToken);
            return $"User {user.Email} is now assigned to role {request.NewRole}.";
        }
    }
}
