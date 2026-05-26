using System.Threading.Tasks;
using Identity.Application.Interfaces;

namespace Identity.Infrastructure.Services
{
    public class EmailServiceAdapter : IEmailService
    {
        private readonly Shared.EmailService.IEmailService _emailService;

        public EmailServiceAdapter(Shared.EmailService.IEmailService emailService)
        {
            _emailService = emailService;
        }

        public Task SendOtpEmailAsync(string toEmail, string userName, string otpCode, int expiryMinutes = 1)
        {
            return _emailService.SendOtpEmailAsync(toEmail, userName, otpCode, expiryMinutes);
        }
    }
}
