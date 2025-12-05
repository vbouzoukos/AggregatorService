using AggregatorService.Models.Responses;

namespace AggregatorService.Services.Provider.Base
{
    /// <summary>
    /// Common interface for all external API providers
    /// </summary>
    public interface IExternalApiProvider
    {
        /// <summary>
        /// Unique name of the provider (e.g., "weather", "news", "twitter")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Checks if the provider can handle the given parameters
        /// </summary>
        /// <param name="parameters">Key-value pairs from the request</param>
        /// <returns>True if provider has required parameters to make a call</returns>
        bool CanHandle(Dictionary<string, string> parameters);

        /// <summary>
        /// Fetches data from the external API
        /// </summary>
        /// <param name="parameters">Key-value pairs from the request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>API response with data or error</returns>
        Task<ApiResponse> FetchAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken);
    }
}