using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;

namespace AggregatorService.Services.Aggregation
{
    /// <summary>
    /// Service responsible for orchestrating calls to multiple external API providers
    /// </summary>
    public interface IAggregationService
    {
        /// <summary>
        /// Aggregates data from all available providers that can handle the given parameters
        /// Calls are executed in parallel for optimal performance
        /// </summary>
        /// <param name="request">Aggregation request containing search parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Aggregated response containing results from all providers</returns>
        Task<AggregationResponse> AggregateAsync(AggregationRequest request, CancellationToken cancellationToken);
    }
}