using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Identity.Application.Interfaces;

namespace Identity.Application.CQRS.Handlers
{
    public class GetAllUsersQueryHandler : IRequestHandler<Queries.GetAllUsersQuery, List<Queries.UserDto>>
    {
        private readonly IIdentityDbContext _context;
        public GetAllUsersQueryHandler(IIdentityDbContext context) => _context = context;

        public async Task<List<Queries.UserDto>> Handle(Queries.GetAllUsersQuery request, CancellationToken cancellationToken)
        {
            var users = await _context.GetAllUsersAsync(cancellationToken);
            return users
                .Select(u => new Queries.UserDto
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    FailedLoginAttempts = u.FailedLoginAttempts,
                    LockoutEnd = u.LockoutEnd
                })
                .ToList();
        }
    }
}
