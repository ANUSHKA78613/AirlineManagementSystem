using FluentValidation;
using Operations.Application.DTOs;

namespace Operations.Application.Validators
{
    public class AddEmployeeDtoValidator : AbstractValidator<AddEmployeeDto>
    {
        public AddEmployeeDtoValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Position).NotEmpty();
        }
    }

    public class CheckInDtoValidator : AbstractValidator<CheckInDto>
    {
        public CheckInDtoValidator()
        {
            RuleFor(x => x.PNR).NotEmpty().WithMessage("PNR is required.");
            RuleFor(x => x.PassengerId).GreaterThan(0);
            RuleFor(x => x.SeatNo).NotEmpty().WithMessage("Seat number is required.");
        }
    }
}
