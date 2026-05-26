namespace Flight.Domain.Entities
{
    public class Seat
    {
        public int SeatId { get; set; }
        public int FlightId { get; set; }
        public string SeatNo { get; set; }
        public bool IsAvailable { get; set; }
        public decimal Price { get; set; }
        public string SeatClass { get; set; } = "Economy";
        public string Category { get; set; } = "Standard";
    }
}
