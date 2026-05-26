namespace EventBus.Events
{
    public class IntegrationEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    }

    public class BookingCreatedEvent : IntegrationEvent
    {
        public string PNR { get; set; }
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PaymentCompletedEvent : IntegrationEvent
    {
        public string PNR { get; set; }
        public int UserId { get; set; }
        public string TransactionId { get; set; }
        public decimal Amount { get; set; }
    }

    public class PaymentFailedEvent : IntegrationEvent
    {
        public string PNR { get; set; }
        public string Reason { get; set; }
        public int UserId { get; set; }
        public int FlightId { get; set; }
    }

    // ──── Eventual Consistency Events ────

    /// <summary>
    /// Published when a flight is created, updated, or its status changes.
    /// The read-model projection handler uses this to keep the read DB in sync.
    /// </summary>
    public class FlightUpdatedEvent : IntegrationEvent
    {
        public int FlightId { get; set; }
        public string FlightNumber { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int AvailableSeats { get; set; }
        public string? GateNumber { get; set; }
    }

    /// <summary>
    /// Published when a booking status transitions (Confirmed → Cancelled, etc.).
    /// Drives the booking read-model projection handler.
    /// </summary>
    public class BookingStatusChangedEvent : IntegrationEvent
    {
        public string PNR { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int PassengerCount { get; set; }
        public string? FlightNumber { get; set; }
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureTime { get; set; }
        public string? UserEmail { get; set; }
    }

    /// <summary>
    /// Published when seat availability changes (booking confirmed/cancelled).
    /// Allows Flight service to keep an accurate seat count in its read model.
    /// </summary>
    public class SeatInventoryChangedEvent : IntegrationEvent
    {
        public int FlightId { get; set; }
        public int SeatsBooked { get; set; }
        public int SeatsReleased { get; set; }
        public List<string> BookedSeatNumbers { get; set; } = new();
        public List<string> ReleasedSeatNumbers { get; set; } = new();
    }
    public class TicketCancelledEvent : IntegrationEvent
    {
        public string PNR { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public int SeatsReleased { get; set; }
        public decimal RefundAmount { get; set; }
        public List<string> ReleasedSeatNumbers { get; set; } = new();
    }

    public class BookingCancelledEvent : IntegrationEvent
    {
        public string PNR { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public int SeatsReleased { get; set; }
        public decimal RefundAmount { get; set; }
        public List<string> ReleasedSeatNumbers { get; set; } = new();
    }

    public class RefundInitiatedEvent : IntegrationEvent
    {
        public string PNR { get; set; } = string.Empty;
        public string RefundId { get; set; } = string.Empty;
        public decimal RefundAmount { get; set; }
        public string TransactionId { get; set; } = string.Empty;
    }

    public class RefundCompletedEvent : IntegrationEvent
    {
        public string PNR { get; set; } = string.Empty;
        public string RefundId { get; set; } = string.Empty;
        public decimal RefundAmount { get; set; }
        public string Status { get; set; } = string.Empty; // Successful, Failed
    }

    public class CommissionGeneratedEvent : IntegrationEvent
    {
        public string PNR { get; set; } = string.Empty;
        public int DealerAgentId { get; set; }
        public string AgentCode { get; set; } = string.Empty;
        public decimal BookingAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal CommissionRate { get; set; }
    }

    public class CouponUsedEvent : IntegrationEvent
    {
        public string CouponCode { get; set; } = string.Empty;
        public string PNR { get; set; } = string.Empty;
        public int UserId { get; set; }
    }
}
