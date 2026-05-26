using MediatR;
using Operations.Application.CQRS.Commands;
using Operations.Infrastructure.Persistence;
using Operations.Domain.Entities;

namespace Operations.API.CQRS.Handlers
{
    public class AddEmployeeCommandHandler : IRequestHandler<AddEmployeeCommand, string>
    {
        private readonly OperationsDbContext _context;
        public AddEmployeeCommandHandler(OperationsDbContext context) { _context = context; }
        public async Task<string> Handle(AddEmployeeCommand request, CancellationToken cancellationToken)
        {
            var e = new Employee { FirstName = request.Dto.FirstName, LastName = request.Dto.LastName, Position = request.Dto.Position };
            _context.Employees.Add(e);
            await _context.SaveChangesAsync(cancellationToken);
            return $"Employee {e.EmployeeCode} added.";
        }
    }
}
