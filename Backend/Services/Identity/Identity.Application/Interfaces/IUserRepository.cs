using Identity.Domain.Entities;

namespace Identity.Application.Interfaces
{
    /// <summary>Repository interface for User aggregate</summary>
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int userId);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByEmailOrPhoneAsync(string emailOrPhone);
        Task<IReadOnlyList<User>> GetAllAsync(string? roleFilter = null);
        Task<bool> ExistsByEmailAsync(string email);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task SaveChangesAsync();
    }
}
