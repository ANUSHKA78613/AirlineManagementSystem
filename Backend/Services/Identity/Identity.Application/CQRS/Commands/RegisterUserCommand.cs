using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.CQRS.Commands
{
    public class RegisterUserCommand : IRequest<string>
    {
        public RegisterDto RegisterDto { get; set; }

        public RegisterUserCommand(RegisterDto registerDto)
        {
            RegisterDto = registerDto;
        }
    }
}
