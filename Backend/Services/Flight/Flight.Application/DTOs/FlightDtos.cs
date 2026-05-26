namespace Flight.Application.DTOs
{
    public class AddFlightDto
    {
        public string FlightNumber { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string SourceTimeZone { get; set; } = "Asia/Kolkata";
        public string DestinationTimeZone { get; set; } = "Asia/Kolkata";
        public int AircraftId { get; set; }
        public int TotalCapacity { get; set; }
        public int EconomySeats { get; set; }
        public decimal EconomyPrice { get; set; }
        public int BusinessSeats { get; set; }
        public decimal BusinessPrice { get; set; }
        public int FirstClassSeats { get; set; }
        public decimal FirstClassPrice { get; set; }
        public decimal WindowSeatCharge { get; set; }
    }

    public class SearchFlightDto
    {
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public DateTime? DepartureDate { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SortBy { get; set; }
    }
}
