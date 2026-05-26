using MediatR;

namespace Booking.Application.CQRS.Queries
{
    public class GetBookingQuery : IRequest<object>
    {
        public string PNR { get; }

        public GetBookingQuery(string pnr)
        {
            PNR = pnr;
        }
    }
}
