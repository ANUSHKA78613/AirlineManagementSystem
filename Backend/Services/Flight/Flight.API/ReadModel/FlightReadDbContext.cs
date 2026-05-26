using Microsoft.EntityFrameworkCore;

namespace Flight.API.ReadModel
{
    /// <summary>
    /// Read-side DbContext for eventual consistency.
    /// Points to a separate read-optimized database (AirlineFlightReadDb).
    /// Only contains denormalized read models — no write operations from commands.
    /// </summary>
    public class FlightReadDbContext : DbContext
    {
        public FlightReadDbContext(DbContextOptions<FlightReadDbContext> options) : base(options) { }

        public DbSet<FlightReadModel> FlightReadModels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FlightReadModel>(entity =>
            {
                entity.HasKey(f => f.FlightId);
                entity.HasIndex(f => f.FlightNumber).IsUnique();
                entity.HasIndex(f => new { f.Source, f.Destination, f.DepartureTime }); // Search index
                entity.HasIndex(f => f.Status);
                entity.Property(f => f.Price).HasPrecision(18, 2);
            });
        }
    }
}
