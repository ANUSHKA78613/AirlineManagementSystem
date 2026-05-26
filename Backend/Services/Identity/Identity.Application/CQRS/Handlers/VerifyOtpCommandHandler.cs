using Identity.Application.CQRS.Commands;
using Identity.Application.Interfaces;
using MediatR;
using Serilog;

namespace Identity.Application.CQRS.Handlers
{
    public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, bool>
    {
        private readonly IOtpService _otpService;

        public VerifyOtpCommandHandler(IOtpService otpService)
        {
            _otpService = otpService;
        }

        public async Task<bool> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
        {
            try
            {
                return await _otpService.VerifyOtpAsync(request.Dto.EmailOrPhone, request.Dto.OtpCode);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in VerifyOtpCommandHandler: {ex.Message}");
                throw;
            }
        }
    }
}
