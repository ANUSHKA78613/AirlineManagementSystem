using System.Threading.Tasks;

namespace Identity.Application.Interfaces
{
    public interface IOtpService
    {
        Task<string> GenerateAndSendOtpAsync(string email, string type);
        Task<bool> VerifyOtpAsync(string email, string otpCode);
    }
}
