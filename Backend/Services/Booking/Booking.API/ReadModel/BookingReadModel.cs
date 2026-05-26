using System;

namespace Booking.API.ReadModel
{
    /// <summary>
    /// Denormalized read model for booking queries.
    /// Combines booking + flight + passenger data for fast, single-query reads.
    /// Projected asynchronously from domain events via RabbitMQ.
    /// </summary>
    public class BookingReadModel
    {
        public int BookingId { get; set; }
        public string PNR { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public string? FlightNumber { get; set; }
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureTime { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal TotalAmount { get; set; }
        public int PassengerCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
