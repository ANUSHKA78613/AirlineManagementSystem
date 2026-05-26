using Flight.Application.DTOs;
using MediatR;

namespace Flight.Application.CQRS.Commands
{
    public class AddFlightCommand : IRequest<string>
    {
        public AddFlightDto Dto { get; }

        public AddFlightCommand(AddFlightDto dto)
        {
            Dto = dto;
        }
    }
}
