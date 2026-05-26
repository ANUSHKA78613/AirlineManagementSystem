using MediatR;
using Operations.Application.DTOs;

namespace Operations.Application.CQRS.Commands
{
    public class CheckInCommand : IRequest<object>
    {
        public CheckInDto Dto { get; }

        public CheckInCommand(CheckInDto dto)
        {
            Dto = dto;
        }
    }
}
