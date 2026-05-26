using Identity.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Interfaces
{
    public interface IIdentityDbContext
    {
        Task<User?> FindUserByIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<User?> FindUserByEmailOrPhoneAsync(string emailOrPhone, CancellationToken cancellationToken = default);
        Task<User?> FindUserByExternalProviderOrEmailAsync(string provider, string providerId, string email, CancellationToken cancellationToken = default);
        Task<bool> UserExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);
        void AddUser(User user);

        Task<RegistrationVerification?> FindRegistrationVerificationByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<RegistrationVerification?> FindVerifiedRegistrationByEmailAsync(string email, CancellationToken cancellationToken = default);
        void AddRegistrationVerification(RegistrationVerification verification);
        void RemoveRegistrationVerification(RegistrationVerification verification);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
