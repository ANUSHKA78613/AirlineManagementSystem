using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace Identity.Infrastructure.Persistence
{
    public static class IdentityDataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentityDbContext>>();

            try
            {
                // Ensure database is created/migrated
                await context.Database.MigrateAsync();

                // 1. Seed Admin
                if (!await context.Users.AnyAsync(u => u.Email == "visheshmadan678@gmail.com"))
                {
                    var admin = new User
                    {
                        Name = "vishesh madan",
                        Email = "visheshmadan678@gmail.com",
                        Phone = "9000000001",
                        Role = "Admin",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPassword@123"),
                        IsVerified = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(admin);
                    logger.LogInformation("Seeding Admin user: {Email}", admin.Email);
                }

                // 2. Seed Requested Staff
                if (!await context.Users.AnyAsync(u => u.Email == "anushkalodhi356@gmail.com"))
                {
                    var staff = new User
                    {
                        Name = "Anushka Lodhi",
                        Email = "anushkalodhi356@gmail.com",
                        Phone = "9000000002",
                        Role = "Staff",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("QWERTYUIOP"),
                        IsVerified = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(staff);
                    logger.LogInformation("Seeding Staff user: {Email}", staff.Email);
                }

                // 3. Seed Demo User
                if (!await context.Users.AnyAsync(u => u.Email == "google.demo.user@gmail.com"))
                {
                    var demoUser = new User
                    {
                        Name = "Google Demo User",
                        Email = "google.demo.user@gmail.com",
                        Phone = "9000000003",
                        Role = "Passenger",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("DemoUser@123"),
                        IsVerified = true,
                        CreatedAt = DateTime.UtcNow,
                        ExternalProvider = "Google",
                        ExternalProviderId = "demo_google_id"
                    };
                    context.Users.Add(demoUser);
                    logger.LogInformation("Seeding Demo User: {Email}", demoUser.Email);
                }
                // 4. Seed Dealer
                if (!await context.Users.AnyAsync(u => u.Email == "anushkalodhi6187@gmail.com"))
                {
                    var dealer = new User
                    {
                        Name = "Anushka Dealer",
                        Email = "anushkalodhi6187@gmail.com",
                        Phone = "9000000004",
                        Role = "Dealer",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("QWERTYUIOP"),
                        IsVerified = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.Users.Add(dealer);
                    logger.LogInformation("Seeding Dealer user: {Email}", dealer.Email);
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }
    }
}
