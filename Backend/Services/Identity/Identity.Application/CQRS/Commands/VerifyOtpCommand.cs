using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.CQRS.Commands
{
    public class VerifyOtpCommand : IRequest<bool>
    {
        public VerifyOtpDto Dto { get; set; }

        public VerifyOtpCommand(VerifyOtpDto dto)
        {
            Dto = dto;
        }
    }
}
