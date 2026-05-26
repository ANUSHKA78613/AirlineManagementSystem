using System;

namespace Booking.Domain.Entities
{
    public class Booking
    {
        public string PNR { get; set; }
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public DateTime BookingDate { get; set; }
        public string Status { get; set; } // Pending, Confirmed, Cancelled
        public decimal TotalAmount { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }

        public Booking()
        {
            PNR = $"PNR{DateTime.UtcNow.Ticks % 100000000}";
            BookingDate = DateTime.UtcNow;
            Status = "Pending";
        }
    }
}
