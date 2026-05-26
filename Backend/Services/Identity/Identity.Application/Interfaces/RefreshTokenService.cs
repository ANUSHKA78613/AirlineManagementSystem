using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Identity.Application.Interfaces
{
    public interface IRefreshTokenService
    {
        Task StoreRefreshTokenAsync(int userId, string refreshToken);
        Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken);
        Task RevokeRefreshTokenAsync(int userId);
    }

    /// <summary>
    /// Stores refresh tokens in Redis (IDistributedCache) instead of the database.
    /// TTL: 7 days. Rotate-on-use: old token is deleted when a new one is issued.
    /// </summary>
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IDistributedCache _cache;
        private static readonly TimeSpan TokenTtl = TimeSpan.FromDays(7);

        public RefreshTokenService(IDistributedCache cache)
        {
            _cache = cache;
        }

        private static string CacheKey(int userId) => $"refresh_token:{userId}";

        public async Task StoreRefreshTokenAsync(int userId, string refreshToken)
        {
            var payload = JsonSerializer.Serialize(new
            {
                Token = refreshToken,
                IssuedAt = DateTime.UtcNow
            });

            await _cache.SetStringAsync(CacheKey(userId), payload, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TokenTtl
            });
        }

        public async Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken)
        {
            var cached = await _cache.GetStringAsync(CacheKey(userId));
            if (string.IsNullOrEmpty(cached)) return false;

            var stored = JsonSerializer.Deserialize<JsonElement>(cached);
            var storedToken = stored.GetProperty("Token").GetString();
            
            return storedToken == refreshToken;
        }

        public async Task RevokeRefreshTokenAsync(int userId)
        {
            await _cache.RemoveAsync(CacheKey(userId));
        }
    }
}
