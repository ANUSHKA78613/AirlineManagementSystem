using FluentValidation;
using Booking.Application.DTOs;

namespace Booking.Application.Validators
{
    public class CreateBookingDtoValidator : AbstractValidator<CreateBookingDto>
    {
        public CreateBookingDtoValidator()
        {
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.FlightId).GreaterThan(0);
            RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Passengers).NotEmpty().WithMessage("At least one passenger is required.");
            RuleForEach(x => x.Passengers).SetValidator(new PassengerDtoValidator());
        }
    }

    public class PassengerDtoValidator : AbstractValidator<PassengerDto>
    {
        public PassengerDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Age).GreaterThan(0).LessThanOrEqualTo(150);
            RuleFor(x => x.Gender).NotEmpty().Must(g => g == "Male" || g == "Female" || g == "Other")
                .WithMessage("Gender must be Male, Female, or Other.");
        }
    }
}
