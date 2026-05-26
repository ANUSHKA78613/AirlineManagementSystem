using System;

namespace Flight.API.ReadModel
{
    /// <summary>
    /// Denormalized read model for flight queries.
    /// Optimized for fast reads; projected asynchronously from domain events.
    /// </summary>
    public class FlightReadModel
    {
        public int FlightId { get; set; }
        public string FlightNumber { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string Status { get; set; } = "Scheduled";
        public decimal Price { get; set; }
        public int AvailableSeats { get; set; }
        public string? GateNumber { get; set; }
        public string SourceTimeZone { get; set; } = "Asia/Kolkata";
        public string DestinationTimeZone { get; set; } = "Asia/Kolkata";
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
