using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IdentityDbContext _context;

        public UserRepository(IdentityDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(int userId)
            => await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

        public async Task<User?> GetByEmailAsync(string email)
            => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public async Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone)
            => await _context.Users.FirstOrDefaultAsync(u => u.Email == emailOrPhone || u.Phone == emailOrPhone);

        public async Task<IReadOnlyList<User>> GetAllAsync(string? roleFilter = null)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(roleFilter))
                query = query.Where(u => u.Role == roleFilter);
            return await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        }

        public async Task<bool> ExistsByEmailAsync(string email)
            => await _context.Users.AnyAsync(u => u.Email == email);

        public async Task AddAsync(User user)
            => await _context.Users.AddAsync(user);

        public Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
            => await _context.SaveChangesAsync();
    }
}
