using Booking.Application.DTOs;
using MediatR;

namespace Booking.Application.CQRS.Commands
{
    public class CreateBookingCommand : IRequest<object>
    {
        public CreateBookingDto Dto { get; }

        public CreateBookingCommand(CreateBookingDto dto)
        {
            Dto = dto;
        }
    }
}
