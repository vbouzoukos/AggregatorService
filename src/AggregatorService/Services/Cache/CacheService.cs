using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AggregatorService.Services.Caching
{
    /// <summary>
    /// Implementation of cache service using IDistributedCache
    /// Currently uses in-memory cache, can be switched to Redis by changing DI registration
    /// </summary>
    /// <remarks>
    /// Future enhancement: Implement a two-tier caching strategy
    /// 1. First check IMemoryCache for quick local access (L1 cache)
    /// 2. If not found, check IDistributedCache/Redis (L2 cache)
    /// 3. If found in Redis, populate local memory cache for subsequent requests
    /// This reduces network calls to Redis for frequently accessed data
    /// </remarks>
    public class CacheService(IDistributedCache distributedCache) : ICacheService
    {
        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var cachedData = await distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(cachedData))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(cachedData);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            var serializedData = JsonSerializer.Serialize(value);
            await distributedCache.SetStringAsync(key, serializedData, options, cancellationToken);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            await distributedCache.RemoveAsync(key, cancellationToken);
        }
    }
}