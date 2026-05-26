using System;

namespace Booking.Domain.Entities
{
    public class Passenger
    {
        public int PassengerId { get; set; }
        public string PNR { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; }
        public string? SeatNo { get; set; }
        public string SeatClass { get; set; } = "Economy"; // Economy, Business, First
    }
}
