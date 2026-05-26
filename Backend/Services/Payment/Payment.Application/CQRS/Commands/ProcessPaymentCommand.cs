using MediatR;
using Payment.Application.DTOs;

namespace Payment.Application.CQRS.Commands
{
    public class ProcessPaymentCommand : IRequest<object>
    {
        public ProcessPaymentDto Dto { get; }

        public ProcessPaymentCommand(ProcessPaymentDto dto)
        {
            Dto = dto;
        }
    }
}
