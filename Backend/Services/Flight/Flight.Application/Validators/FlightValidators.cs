using FluentValidation;
using Flight.Application.DTOs;

namespace Flight.Application.Validators
{
    public class AddFlightDtoValidator : AbstractValidator<AddFlightDto>
    {
        public AddFlightDtoValidator()
        {
            RuleFor(x => x.FlightNumber).NotEmpty().MaximumLength(10);
            RuleFor(x => x.Source).NotEmpty().MaximumLength(10);
            RuleFor(x => x.Destination).NotEmpty().MaximumLength(10)
                .NotEqual(x => x.Source).WithMessage("Source and destination cannot be the same.");
            RuleFor(x => x.DepartureTime).GreaterThan(DateTime.UtcNow).WithMessage("Departure must be in the future.");
            RuleFor(x => x.ArrivalTime).GreaterThan(x => x.DepartureTime).WithMessage("Arrival must be after departure.");
            RuleFor(x => x.TotalCapacity).GreaterThan(0).LessThanOrEqualTo(500);
        }
    }
}
