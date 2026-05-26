using MediatR;

namespace Flight.Application.CQRS.Commands
{
    public class UpdateFlightStatusCommand : IRequest<string>
    {
        public int FlightId { get; }
        public string Status { get; }

        public UpdateFlightStatusCommand(int flightId, string status)
        {
            FlightId = flightId;
            Status = status;
        }
    }
}
