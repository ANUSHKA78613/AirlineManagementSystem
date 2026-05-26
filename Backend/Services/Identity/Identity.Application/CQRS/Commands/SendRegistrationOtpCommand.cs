using MediatR;
using System.ComponentModel.DataAnnotations;

namespace Identity.Application.CQRS.Commands
{
    public class SendRegistrationOtpCommand : IRequest<string>
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public SendRegistrationOtpCommand(string email)
        {
            Email = email;
        }
    }
}
