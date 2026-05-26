using System.Threading.Tasks;

namespace Identity.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string userName, string otpCode, int expiryMinutes = 5);
    }
}
