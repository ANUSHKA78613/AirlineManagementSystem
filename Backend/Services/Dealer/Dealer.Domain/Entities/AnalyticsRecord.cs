using System;
namespace Dealer.Domain.Entities
{
    public class AnalyticsRecord
    {
        public int RecordId { get; set; }
        public string EventType { get; set; } // BookingCreated, PaymentProcessed, FlightCancelled
        public string EntityId { get; set; }
        public string EntityType { get; set; } // Booking, Payment, Flight
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime EventDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public AnalyticsRecord() { CreatedAt = DateTime.UtcNow; EventDate = DateTime.UtcNow; }
    }
}
