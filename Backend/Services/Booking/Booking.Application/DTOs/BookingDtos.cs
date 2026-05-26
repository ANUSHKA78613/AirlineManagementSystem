namespace Booking.Application.DTOs
{
    public class CreateBookingDto
    {
        public int UserId { get; set; }
        public int FlightId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<PassengerDto> Passengers { get; set; } = new();
        public string? CouponCode { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureTime { get; set; }
        public DateTime? ArrivalTime { get; set; }
    }

    public class PassengerDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? SeatNo { get; set; }
        public string SeatClass { get; set; } = "Economy";
    }
}
