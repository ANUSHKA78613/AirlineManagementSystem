using MediatR;
using Operations.Application.DTOs;

namespace Operations.Application.CQRS.Commands
{
    public class AddEmployeeCommand : IRequest<string>
    {
        public AddEmployeeDto Dto { get; }

        public AddEmployeeCommand(AddEmployeeDto dto)
        {
            Dto = dto;
        }
    }
}
