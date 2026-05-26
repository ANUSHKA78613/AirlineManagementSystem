using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.CQRS.Queries
{
    public class LoginUserQuery : IRequest<string>
    {
        public LoginDto LoginDto { get; set; }

        public LoginUserQuery(LoginDto loginDto)
        {
            LoginDto = loginDto;
        }
    }
}
