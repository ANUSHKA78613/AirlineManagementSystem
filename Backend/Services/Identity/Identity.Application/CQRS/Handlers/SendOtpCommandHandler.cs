using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using MediatR;
using Serilog;

namespace Identity.Application.CQRS.Handlers
{
    public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand, string>
    {
        private readonly IOtpService _otpService;

        public SendOtpCommandHandler(IOtpService otpService)
        {
            _otpService = otpService;
        }

        public async Task<string> Handle(SendOtpCommand request, CancellationToken cancellationToken)
        {
            try
            {
                bool isPhone = request.Dto.EmailOrPhone.All(char.IsDigit) || request.Dto.EmailOrPhone.StartsWith("+");
                var type = isPhone ? "phone" : "email";
                return await _otpService.GenerateAndSendOtpAsync(request.Dto.EmailOrPhone, type);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in SendOtpCommandHandler: {ex.Message}");
                throw;
            }
        }
    }
}
