using Identity.Application.DTOs;
using MediatR;

namespace Identity.Application.CQRS.Commands
{
    public class SendOtpCommand : IRequest<string>
    {
        public SendOtpDto Dto { get; set; }

        public SendOtpCommand(SendOtpDto dto)
        {
            Dto = dto;
        }
    }
}
