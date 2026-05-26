using FluentValidation;
using Identity.Application.DTOs;

namespace Identity.Application.Validators
{
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        private static readonly string[] ValidRoles = { "Passenger", "Admin", "Staff", "Dealer" };

        public RegisterDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^\+?[\d\-]{7,15}$").WithMessage("Invalid phone number format.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[\d]").WithMessage("Password must contain at least one digit.");

            RuleFor(x => x.Role)
                .Must(r => ValidRoles.Contains(r))
                .WithMessage($"Role must be one of: {string.Join(", ", ValidRoles)}.");
        }
    }

    public class LoginDtoValidator : AbstractValidator<LoginDto>
    {
        public LoginDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
    {
        public ForgotPasswordDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.OtpCode).NotEmpty().Length(6);
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
        }
    }

    public class SendOtpDtoValidator : AbstractValidator<SendOtpDto>
    {
        public SendOtpDtoValidator()
        {
            RuleFor(x => x.EmailOrPhone).NotEmpty().WithMessage("Email or phone is required.");
        }
    }

    public class VerifyOtpDtoValidator : AbstractValidator<VerifyOtpDto>
    {
        public VerifyOtpDtoValidator()
        {
            RuleFor(x => x.EmailOrPhone).NotEmpty();
            RuleFor(x => x.OtpCode).NotEmpty().Length(6);
        }
    }
}
