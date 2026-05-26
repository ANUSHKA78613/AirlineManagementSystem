using FluentValidation;
using Payment.Application.DTOs;

namespace Payment.Application.Validators
{
    public class ProcessPaymentDtoValidator : AbstractValidator<ProcessPaymentDto>
    {
        public ProcessPaymentDtoValidator()
        {
            RuleFor(x => x.PNR).NotEmpty().WithMessage("PNR is required.");
            RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
            RuleFor(x => x.PaymentMethod).NotEmpty();
        }
    }
}
