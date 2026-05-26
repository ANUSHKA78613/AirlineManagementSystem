using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.CQRS.Commands
{
    public class ForgotPasswordCommand : IRequest<string>
    {
        public ForgotPasswordDto Dto { get; set; }
        public ForgotPasswordCommand(ForgotPasswordDto dto) { Dto = dto; }
    }
}
