namespace Flight.Domain.Entities
{
    public class DynamicPricing
    {
        public int RuleId { get; set; }
        public int FlightId { get; set; }
        public double DemandFactor { get; set; }
        public double PriceMultiplier { get; set; }
    }
}
