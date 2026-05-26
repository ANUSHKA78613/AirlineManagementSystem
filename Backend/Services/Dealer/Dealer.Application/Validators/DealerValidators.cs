using FluentValidation;
using Dealer.Application.DTOs;

namespace Dealer.Application.Validators
{
    public class RegisterDealerDtoValidator : AbstractValidator<RegisterDealerDto>
    {
        public RegisterDealerDtoValidator()
        {
            RuleFor(x => x.AgentName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.AgencyName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.AgentCode).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Phone).NotEmpty();
            RuleFor(x => x.CommissionRate).InclusiveBetween(0, 1)
                .WithMessage("Commission rate must be between 0 and 1 (0-100%).");
        }
    }

    public class WalletOperationDtoValidator : AbstractValidator<WalletOperationDto>
    {
        public WalletOperationDtoValidator()
        {
            RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than zero.");
        }
    }

    public class CommissionRequestDtoValidator : AbstractValidator<CommissionRequestDto>
    {
        public CommissionRequestDtoValidator()
        {
            RuleFor(x => x.AgentId).GreaterThan(0);
            RuleFor(x => x.AgentCode).NotEmpty();
            RuleFor(x => x.BookingAmount).GreaterThan(0);
        }
    }
}
