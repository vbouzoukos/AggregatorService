namespace AggregatorService.Services.Caching
{
    /// <summary>
    /// Abstraction for distributed cache operations
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Gets a value from cache
        /// </summary>
        /// <typeparam name="T">Type of cached value</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached value or default if not found</returns>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a value in cache
        /// </summary>
        /// <typeparam name="T">Type of value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Time to live</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a value from cache
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    }
}