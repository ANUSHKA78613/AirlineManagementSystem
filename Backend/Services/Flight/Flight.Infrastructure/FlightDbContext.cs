using Microsoft.EntityFrameworkCore;
using Flight.Domain.Entities;

namespace Flight.Infrastructure.Persistence
{
    public class FlightDbContext : DbContext
    {
        public FlightDbContext(DbContextOptions<FlightDbContext> options) : base(options) { }

        // Flight entities
        public DbSet<Flight.Domain.Entities.Flight> Flights { get; set; }
        public DbSet<Route> Routes { get; set; }
        public DbSet<Airport> Airports { get; set; }

        // Inventory entities (merged)
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Seat> Seats { get; set; }

        // Pricing entities (merged)
        public DbSet<FlightPricing> FlightPricings { get; set; }
        public DbSet<SeatConfiguration> SeatConfigurations { get; set; }
        public DbSet<DynamicPricing> DynamicPricingRules { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsage> CouponUsages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Flight
            modelBuilder.Entity<Flight.Domain.Entities.Flight>().HasKey(f => f.FlightId);
            modelBuilder.Entity<Flight.Domain.Entities.Flight>().HasIndex(f => f.FlightNumber).IsUnique();

            modelBuilder.Entity<Route>(entity =>
            {
                entity.HasKey(r => r.RouteId);
                entity.HasIndex(r => new { r.Source, r.Destination }).IsUnique();
            });

            modelBuilder.Entity<Airport>(entity =>
            {
                entity.HasKey(a => a.AirportId);
                entity.HasIndex(a => a.Code).IsUnique();
            });

            // Inventory
            modelBuilder.Entity<InventoryItem>().HasKey(i => i.ItemId);
            modelBuilder.Entity<InventoryItem>().HasIndex(i => i.SKU).IsUnique();
            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Seat>().HasKey(s => s.SeatId);
            modelBuilder.Entity<Seat>()
                .Property(s => s.Price)
                .HasPrecision(18, 2);

            // Pricing
            modelBuilder.Entity<FlightPricing>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.BasePrice).HasPrecision(18, 2);
                entity.Property(p => p.Multiplier).HasPrecision(18, 2);
                entity.Property(p => p.WindowSeatCharge).HasPrecision(18, 2);
            });

            modelBuilder.Entity<SeatConfiguration>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.AisleMarkup).HasPrecision(18, 2);
                entity.Property(s => s.WindowMarkup).HasPrecision(18, 2);
                entity.Property(s => s.MiddleMarkup).HasPrecision(18, 2);
            });
            modelBuilder.Entity<DynamicPricing>().HasKey(d => d.RuleId);

            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.HasKey(c => c.CouponId);
                entity.HasIndex(c => c.Code).IsUnique();
                entity.Property(c => c.DiscountPercent).HasColumnType("decimal(5,2)");
            });

            modelBuilder.Entity<CouponUsage>(entity =>
            {
                entity.HasKey(cu => cu.CouponUsageId);
                entity.HasIndex(cu => new { cu.CouponCode, cu.UserId });
            });
        }
    }
}
