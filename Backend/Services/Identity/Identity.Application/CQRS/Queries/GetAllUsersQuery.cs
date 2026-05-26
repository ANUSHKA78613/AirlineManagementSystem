using MediatR;
using System.Collections.Generic;
using Identity.Application.DTOs;

namespace Identity.Application.CQRS.Queries
{
    public class GetAllUsersQuery : IRequest<List<UserDto>>
    {
    }

    public class UserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public int FailedLoginAttempts { get; set; }
        public System.DateTime? LockoutEnd { get; set; }
    }
}
