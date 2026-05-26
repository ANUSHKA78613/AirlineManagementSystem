using Microsoft.EntityFrameworkCore;

namespace Booking.API.ReadModel
{
    /// <summary>
    /// Read-side DbContext for eventual consistency.
    /// Points to a separate read-optimized database (AirlineBookingReadDb).
    /// Only contains denormalized read models — no write operations from commands.
    /// </summary>
    public class BookingReadDbContext : DbContext
    {
        public BookingReadDbContext(DbContextOptions<BookingReadDbContext> options) : base(options) { }

        public DbSet<BookingReadModel> BookingReadModels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BookingReadModel>(entity =>
            {
                entity.HasKey(b => b.BookingId);
                entity.HasIndex(b => b.PNR).IsUnique();
                entity.HasIndex(b => b.UserId);
                entity.HasIndex(b => b.Status);
                entity.HasIndex(b => b.FlightId);
                entity.Property(b => b.TotalAmount).HasPrecision(18, 2);
            });
        }
    }
}
