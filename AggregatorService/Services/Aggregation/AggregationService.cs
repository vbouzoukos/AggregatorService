using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Providers.Base;
using System.Diagnostics;

namespace AggregatorService.Services.Aggregation
{
    /// <summary>
    /// Service that orchestrates parallel calls to multiple external API providers
    /// and aggregates their responses
    /// </summary>
    public class AggregationService(
        IEnumerable<IExternalApiProvider> providers,
        ILogger<AggregationService> logger) : IAggregationService
    {
        public async Task<AggregationResponse> AggregateAsync(
            AggregationRequest request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            // Filter providers that can handle the given parameters
            var applicableProviders = providers
                .Where(p => p.CanHandle(request.Parameters))
                .ToList();

            logger.LogInformation(
                "Aggregating data from {Count} providers for parameters: {Parameters}",
                applicableProviders.Count,
                string.Join(", ", request.Parameters.Keys));

            if (applicableProviders.Count == 0)
            {
                logger.LogWarning("No providers can handle the given parameters");
                return new AggregationResponse
                {
                    TotalResponseTime = stopwatch.Elapsed,
                    ProvidersQueried = 0,
                    SuccessfulResponses = 0
                };
            }

            // Execute all provider calls in parallel
            var tasks = applicableProviders
                .Select(provider => FetchFromProviderAsync(provider, request.Parameters, cancellationToken))
                .ToList();

            var results = await Task.WhenAll(tasks);

            stopwatch.Stop();

            var response = new AggregationResponse
            {
                TotalResponseTime = stopwatch.Elapsed,
                ProvidersQueried = applicableProviders.Count,
                SuccessfulResponses = results.Count(r => r.IsSuccess),
                Results = results.ToList()
            };

            logger.LogInformation(
                "Aggregation completed in {ElapsedMs}ms. Success: {Success}/{Total}",
                stopwatch.ElapsedMilliseconds,
                response.SuccessfulResponses,
                response.ProvidersQueried);

            return response;
        }

        /// <summary>
        /// Fetches data from a single provider with error handling
        /// Individual provider failures do not affect other providers
        /// </summary>
        private async Task<ApiResponse> FetchFromProviderAsync(
            IExternalApiProvider provider,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug("Calling provider: {Provider}", provider.Name);
                return await provider.FetchAsync(parameters, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching from provider {Provider}", provider.Name);

                return new ApiResponse
                {
                    Provider = provider.Name,
                    IsSuccess = false,
                    ErrorMessage = $"Provider error: {ex.Message}",
                    ResponseTime = TimeSpan.Zero
                };
            }
        }
    }
}