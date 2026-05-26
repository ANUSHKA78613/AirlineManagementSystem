using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Flight.Application.CQRS.Commands;
using Flight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Middleware.Exceptions;

namespace Flight.API.CQRS.Handlers
{
    public class UpdateFlightStatusCommandHandler : IRequestHandler<UpdateFlightStatusCommand, string>
    {
        private readonly FlightDbContext _context;
        public UpdateFlightStatusCommandHandler(FlightDbContext context) { _context = context; }

        public async Task<string> Handle(UpdateFlightStatusCommand request, CancellationToken cancellationToken)
        {
            var flight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightId == request.FlightId, cancellationToken);
            if (flight == null) throw new NotFoundException("Flight", request.FlightId);

            flight.Status = request.Status;
            await _context.SaveChangesAsync(cancellationToken);
            return $"Flight {flight.FlightNumber} status updated to '{request.Status}'.";
        }
    }
}
