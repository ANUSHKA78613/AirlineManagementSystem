using System;

namespace Flight.Domain.Entities
{
    public class Flight
    {
        public int FlightId { get; set; }
        public string FlightNumber { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public DateTime DepartureTime { get; set; }   // Stored in UTC
        public DateTime ArrivalTime { get; set; }      // Stored in UTC
        public string SourceTimeZone { get; set; } = "Asia/Kolkata";       // IANA timezone
        public string DestinationTimeZone { get; set; } = "Asia/Kolkata";  // IANA timezone
        public int AircraftId { get; set; }
        public string Status { get; set; } // Scheduled, Cancelled
        public int AvailableSeats { get; set; } = 180;
        public string? GateNumber { get; set; }

        public Flight()
        {
            Status = "Scheduled";
        }
    }
}
