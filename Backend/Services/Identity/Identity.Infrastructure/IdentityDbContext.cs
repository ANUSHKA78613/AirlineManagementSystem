using Microsoft.EntityFrameworkCore;
using Identity.Domain.Entities;
using Identity.Application.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace Identity.Infrastructure.Persistence
{
    public class IdentityDbContext : DbContext, Identity.Application.Interfaces.IIdentityDbContext
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<RegistrationVerification> RegistrationVerifications { get; set; }

        public Task<User?> FindUserByIdAsync(int userId, CancellationToken cancellationToken = default)
            => Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        public Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        public Task<User?> FindUserByEmailOrPhoneAsync(string emailOrPhone, CancellationToken cancellationToken = default)
            => Users.FirstOrDefaultAsync(u => u.Email == emailOrPhone || u.Phone == emailOrPhone, cancellationToken);

        public Task<User?> FindUserByExternalProviderOrEmailAsync(string provider, string providerId, string email, CancellationToken cancellationToken = default)
            => Users.FirstOrDefaultAsync(u => (u.ExternalProvider == provider && u.ExternalProviderId == providerId) || u.Email == email, cancellationToken);

        public Task<bool> UserExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Users.AnyAsync(u => u.Email == email, cancellationToken);

        public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default)
            => Users.ToListAsync(cancellationToken);

        public void AddUser(User user) => Users.Add(user);

        public Task<RegistrationVerification?> FindRegistrationVerificationByEmailAsync(string email, CancellationToken cancellationToken = default)
            => RegistrationVerifications.FirstOrDefaultAsync(v => v.Email == email, cancellationToken);

        public Task<RegistrationVerification?> FindVerifiedRegistrationByEmailAsync(string email, CancellationToken cancellationToken = default)
            => RegistrationVerifications.FirstOrDefaultAsync(v => v.Email == email && v.IsVerified, cancellationToken);

        public void AddRegistrationVerification(RegistrationVerification verification) => RegistrationVerifications.Add(verification);

        public void RemoveRegistrationVerification(RegistrationVerification verification) => RegistrationVerifications.Remove(verification);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().HasKey(u => u.UserId);
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        }
    }
}
