namespace Flight.Domain.Entities
{
    public class FlightPricing
    {
        public int Id { get; set; }
        public int FlightId { get; set; }
        public string Class { get; set; } = "Economy"; // Economy, Business, First
        public decimal BasePrice { get; set; }
        public decimal Multiplier { get; set; } // e.g. 1.0 (Economy), 1.8 (Business), 3.0 (First)
        public decimal WindowSeatCharge { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
