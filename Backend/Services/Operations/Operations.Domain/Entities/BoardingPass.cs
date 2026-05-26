using System;

namespace Operations.Domain.Entities
{
    public class BoardingPass
    {
        public int BoardingPassId { get; set; }
        public int PassengerId { get; set; }
        public string PNR { get; set; }
        public string SeatNo { get; set; }
        public string Gate { get; set; }
        public DateTime BoardingTime { get; set; }
        public string QRCode { get; set; } // Cryptographic hash simulating barcode payload
    }
}
