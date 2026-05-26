using MediatR;
using System.ComponentModel.DataAnnotations;

namespace Identity.Application.CQRS.Commands
{
    public class VerifyRegistrationOtpCommand : IRequest<bool>
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string OtpCode { get; set; }

        public VerifyRegistrationOtpCommand(string email, string otpCode)
        {
            Email = email;
            OtpCode = otpCode;
        }
    }
}
