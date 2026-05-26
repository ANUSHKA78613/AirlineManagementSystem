using System;

namespace Flight.Domain.Entities
{
    public class Route
    {
        public int RouteId { get; set; }
        public string Source { get; set; } = string.Empty;        // IATA code e.g. DEL
        public string Destination { get; set; } = string.Empty;   // IATA code e.g. BOM
        public int DistanceKm { get; set; }
        public string EstimatedDuration { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
