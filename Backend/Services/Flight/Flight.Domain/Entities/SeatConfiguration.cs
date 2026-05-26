namespace Flight.Domain.Entities
{
    public class SeatConfiguration
    {
        public int Id { get; set; }
        public int FlightId { get; set; }
        public string Class { get; set; } = "Economy"; // Economy, Business, First
        public int TotalCapacity { get; set; }
        public int TotalRows { get; set; }
        public string ColumnsLayout { get; set; } = "3-3"; // "3-3", "2-2", "1-2-1"
        public decimal AisleMarkup { get; set; } = 0;
        public decimal WindowMarkup { get; set; } = 0;
        public decimal MiddleMarkup { get; set; } = 0;
    }
}
