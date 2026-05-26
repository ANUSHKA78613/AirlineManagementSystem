namespace Operations.Application.DTOs
{
    public class AddEmployeeDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
    }

    public class CheckInDto
    {
        public string PNR { get; set; } = string.Empty;
        public int PassengerId { get; set; }
        public string SeatNo { get; set; } = string.Empty;
        public string PassengerName { get; set; } = "Passenger";
        public string Email { get; set; } = string.Empty;
        public DateTime? FlightDepartureTime { get; set; }
    }
}
