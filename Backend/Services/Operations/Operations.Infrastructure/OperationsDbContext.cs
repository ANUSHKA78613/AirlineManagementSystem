using Microsoft.EntityFrameworkCore;
using Operations.Domain.Entities;

namespace Operations.Infrastructure.Persistence
{
    public class OperationsDbContext : DbContext
    {
        public OperationsDbContext(DbContextOptions<OperationsDbContext> options) : base(options) { }

        // Operations entities
        public DbSet<Employee> Employees { get; set; }
        public DbSet<CheckIn> CheckIns { get; set; }
        public DbSet<BoardingPass> BoardingPasses { get; set; }
        public DbSet<Baggage> BaggageRecords { get; set; }
        public DbSet<Issue> Issues { get; set; }

        // Notification entities (merged)
        public DbSet<NotificationRecord> Notifications { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Employee>().HasKey(e => e.EmployeeId);
            modelBuilder.Entity<Employee>().HasIndex(e => e.EmployeeCode).IsUnique();

            modelBuilder.Entity<CheckIn>().HasKey(c => c.CheckInId);
            modelBuilder.Entity<BoardingPass>().HasKey(b => b.BoardingPassId);

            modelBuilder.Entity<Baggage>(entity =>
            {
                entity.HasKey(b => b.BaggageId);
                entity.HasIndex(b => b.TagNumber).IsUnique();
                entity.HasIndex(b => b.PNR);
                entity.Property(b => b.Weight).HasColumnType("decimal(8,2)");
            });

            modelBuilder.Entity<Issue>(entity =>
            {
                entity.HasKey(i => i.IssueId);
                entity.HasIndex(i => i.PNR);
            });

            // Notification
            modelBuilder.Entity<NotificationRecord>(entity =>
            {
                entity.HasKey(n => n.NotificationId);
            });

        }
    }
}
