using MediatR;
using Booking.Application.CQRS.Queries;
using Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Middleware.Exceptions;

namespace Booking.API.CQRS.Handlers
{
    public class GetBookingQueryHandler : IRequestHandler<GetBookingQuery, object>
    {
        private readonly BookingDbContext _context;
        public GetBookingQueryHandler(BookingDbContext context) { _context = context; }

        public async Task<object> Handle(GetBookingQuery request, CancellationToken cancellationToken)
        {
            var booking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.PNR == request.PNR, cancellationToken);
            if (booking == null) throw new NotFoundException("Booking", request.PNR);

            var passengers = await _context.Passengers.AsNoTracking().Where(p => p.PNR == request.PNR).ToListAsync(cancellationToken);

            // explicitly map to camelCase as expected by frontend
            return new {
                booking = new {
                    pnr = booking.PNR,
                    userId = booking.UserId,
                    flightId = booking.FlightId,
                    bookingDate = booking.BookingDate,
                    status = booking.Status,
                    totalAmount = booking.TotalAmount,
                    userEmail = booking.UserEmail,
                    userName = booking.UserName
                },
                passengers = passengers.Select(p => new {
                    passengerId = p.PassengerId,
                    pnr = p.PNR,
                    name = p.Name,
                    age = p.Age,
                    gender = p.Gender,
                    seatNo = p.SeatNo,
                    seatClass = p.SeatClass
                }).ToList()
            };
        }
    }
}
