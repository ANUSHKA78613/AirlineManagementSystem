using MediatR;

namespace Booking.Application.CQRS.Commands
{
    public class CancelBookingResult
    {
        public decimal RefundAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CancelBookingCommand : IRequest<CancelBookingResult>
    {
        public string PNR { get; }
        public List<int>? PassengerIds { get; }

        public CancelBookingCommand(string pnr, List<int>? passengerIds = null)
        {
            PNR = pnr;
            PassengerIds = passengerIds;
        }
    }
}
