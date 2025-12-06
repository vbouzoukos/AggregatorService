using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;

namespace AggregatorService.Services.Provider.Base
{
    /// <summary>
    /// Common interface for all external API providers
    /// </summary>
    public interface IExternalApiProvider
    {
        /// <summary>
        /// Unique name of the provider (e.g., "Weather", "News", "Books")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Checks if the provider can handle the given request.
        /// Uses Required config to determine if any required parameter is present
        /// in either the filters or parameters.
        /// </summary>
        /// <param name="request">Aggregation request with filters and parameters</param>
        /// <returns>True if provider has at least one required parameter</returns>
        bool CanHandle(AggregationRequest request);

        /// <summary>
        /// Fetches data from the external API
        /// </summary>
        /// <param name="request">Aggregation request with filters, sort, and parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>API response with data or error</returns>
        Task<ApiResponse> FetchAsync(AggregationRequest request, CancellationToken cancellationToken);
    }
}