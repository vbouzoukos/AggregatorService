using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Statistics;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AggregatorService.Services.Provider
{
    /// <summary>
    /// Provider for Open Library Search API
    /// Filters, sort mappings, and required parameters configured in appsettings.json
    /// </summary>
    public class OpenLibraryProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cacheService,
        IStatisticsService statisticsService,
        ILogger<OpenLibraryProvider> logger) : IExternalApiProvider
    {
        private const string CacheKeyPrefix = "books:";
        private const string ConfigKey = "ExternalApis:OpenLibrary";

        public string Name => "Books";

        private readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue($"{ConfigKey}:CacheMinutes", 30));

        public bool CanHandle(AggregationRequest request)
        {
            var required = configuration.GetSection($"{ConfigKey}:Required").Get<string[]>() ?? [];
            var filters = configuration.GetSection($"{ConfigKey}:Filters").Get<Dictionary<string, string>>() ?? [];

            // Check if any required parameter exists with a value in filters or parameters
            foreach (var req in required)
            {
                // Check if required param is satisfied by a filter
                var filterMatch = filters.FirstOrDefault(f => f.Value.Equals(req, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(filterMatch.Key))
                {
                    var filterValue = GetFilterValue(request, filterMatch.Key);
                    if (!string.IsNullOrEmpty(filterValue))
                    {
                        return true;
                    }
                }

                // Check if required param exists in parameters with a value
                if (request.Parameters.TryGetValue(req, out var paramValue) && !string.IsNullOrEmpty(paramValue))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<ApiResponse> FetchAsync(AggregationRequest request, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Build working parameters from filters and request parameters
                var workingParams = BuildWorkingParameters(request);

                // Apply sort mapping
                ApplySortMapping(workingParams, request.Sort);

                var booksData = await GetBooksDataAsync(workingParams, cancellationToken);
                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, true);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = true,
                    Data = booksData,
                    ResponseTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching {Name} data", Name);
                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, false);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ResponseTime = stopwatch.Elapsed
                };
            }
        }

        private Dictionary<string, string> BuildWorkingParameters(AggregationRequest request)
        {
            var workingParams = new Dictionary<string, string>(request.Parameters, StringComparer.OrdinalIgnoreCase);
            var filters = configuration.GetSection($"{ConfigKey}:Filters").Get<Dictionary<string, string>>() ?? [];

            // Apply filters from request to working parameters
            foreach (var filter in filters)
            {
                var filterValue = GetFilterValue(request, filter.Key);
                if (!string.IsNullOrEmpty(filterValue))
                {
                    workingParams[filter.Value] = filterValue;
                }
            }

            return workingParams;
        }

        private static string? GetFilterValue(AggregationRequest request, string filterName)
        {
            return filterName.ToLowerInvariant() switch
            {
                "query" => request.Query,
                "country" => request.Country,
                "language" => request.Language,
                _ => null
            };
        }

        private void ApplySortMapping(Dictionary<string, string> parameters, SortOption sort)
        {
            var sortParameter = configuration[$"{ConfigKey}:SortParameter"];
            var sortMappings = configuration.GetSection($"{ConfigKey}:SortMappings")
                .Get<Dictionary<string, string?>>();

            if (string.IsNullOrEmpty(sortParameter) || sortMappings == null)
            {
                logger.LogDebug("{Name} does not support sorting", Name);
                return;
            }

            var sortKey = sort.ToString();
            if (sortMappings.TryGetValue(sortKey, out var apiSortValue) && !string.IsNullOrEmpty(apiSortValue))
            {
                parameters[sortParameter] = apiSortValue;
                logger.LogDebug("Applied sort mapping: {Sort} -> {Param}={Value}", sort, sortParameter, apiSortValue);
            }
        }

        private async Task<dynamic?> GetBooksDataAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var queryString = BuildQueryString(parameters);
            var cacheKey = $"{CacheKeyPrefix}{queryString}".ToLowerInvariant();

            var cached = await cacheService.GetAsync<JsonElement>(cacheKey, cancellationToken);
            if (cached.ValueKind != JsonValueKind.Undefined)
            {
                logger.LogDebug("Books cache hit for {CacheKey}", cacheKey);
                return cached;
            }

            var baseUrl = configuration[$"{ConfigKey}:Url"];
            var url = $"{baseUrl}?{queryString}";

            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var booksData = JsonSerializer.Deserialize<JsonElement>(content);

            await cacheService.SetAsync(cacheKey, booksData, cacheExpiration, cancellationToken);
            logger.LogDebug("Cached books data for {CacheKey}", cacheKey);

            return booksData;
        }

        private string BuildQueryString(Dictionary<string, string> parameters)
        {
            var queryBuilder = new StringBuilder();

            // Get all parameter keys: filters + configured parameters
            var filters = configuration.GetSection($"{ConfigKey}:Filters").Get<Dictionary<string, string>>() ?? [];
            var apiParams = configuration.GetSection($"{ConfigKey}:Parameters").Get<string[]>() ?? [];

            // Combine filter values and api params
            var allParams = filters.Values.Concat(apiParams).Distinct();

            foreach (var param in allParams)
            {
                if (parameters.TryGetValue(param, out var paramValue) && !string.IsNullOrEmpty(paramValue))
                {
                    if (queryBuilder.Length > 0)
                    {
                        queryBuilder.Append('&');
                    }
                    queryBuilder.Append($"{param}={Uri.EscapeDataString(paramValue)}");
                }
            }

            return queryBuilder.ToString();
        }
    }
}