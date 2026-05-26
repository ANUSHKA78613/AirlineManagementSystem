using Microsoft.EntityFrameworkCore;
using Booking.Domain.Entities;

namespace Booking.Infrastructure.Persistence
{
    public class BookingDbContext : DbContext
    {
        public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }
        public DbSet<Booking.Domain.Entities.Booking> Bookings { get; set; }
        public DbSet<Passenger> Passengers { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Booking.Domain.Entities.Booking>(entity =>
            {
                entity.HasKey(b => b.PNR);
                entity.Property(b => b.TotalAmount).HasPrecision(18, 2);
            });
            modelBuilder.Entity<Passenger>().HasKey(p => p.PassengerId);
        }
    }
}
