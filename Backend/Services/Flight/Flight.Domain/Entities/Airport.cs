using System;

namespace Flight.Domain.Entities
{
    public class Airport
    {
        public int AirportId { get; set; }
        public string Code { get; set; } = string.Empty;    // IATA e.g. DEL
        public string Name { get; set; } = string.Empty;    // Indira Gandhi International
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
