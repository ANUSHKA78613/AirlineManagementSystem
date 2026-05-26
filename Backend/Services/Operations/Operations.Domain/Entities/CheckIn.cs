using System;

namespace Operations.Domain.Entities
{
    public class CheckIn
    {
        public int CheckInId { get; set; }
        public int PassengerId { get; set; }
        public string PNR { get; set; }
        public string SeatNo { get; set; }
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } // CheckedIn, Pending
    }
}
