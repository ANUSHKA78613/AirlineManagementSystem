using MediatR;
using Identity.Application.DTOs;

namespace Identity.Application.CQRS.Commands
{
    public class VerifyRegistrationCommand : IRequest<bool>
    {
        public VerifyOtpDto Dto { get; }

        public VerifyRegistrationCommand(VerifyOtpDto dto)
        {
            Dto = dto;
        }
    }
}
