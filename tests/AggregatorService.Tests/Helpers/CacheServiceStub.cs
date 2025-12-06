using AggregatorService.Services.Caching;
namespace AggregatorService.Tests.Helpers
{
    /// <summary>
    /// Type-safe cache service stub that tracks all cache operations
    /// </summary>
    public class CacheServiceStub : ICacheService
    {
        private readonly Dictionary<string, object?> cache = [];
        private readonly List<string> getKeys = [];
        private readonly List<string> setKeys = [];

        public IReadOnlyList<string> GetKeys => getKeys;
        public IReadOnlyList<string> SetKeys => setKeys;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            getKeys.Add(key);

            if (cache.TryGetValue(key, out var value) && value is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
            }

            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            setKeys.Add(key);
            cache[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}