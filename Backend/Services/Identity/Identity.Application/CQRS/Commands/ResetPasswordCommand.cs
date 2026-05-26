using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.CQRS.Commands
{
    public class ResetPasswordCommand : IRequest<string>
    {
        public ResetPasswordDto Dto { get; set; }
        public ResetPasswordCommand(ResetPasswordDto dto) { Dto = dto; }
    }
}
