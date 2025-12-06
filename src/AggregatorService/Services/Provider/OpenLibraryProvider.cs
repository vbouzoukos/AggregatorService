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
    /// Supported parameters:
    /// - q: Search query (required)
    /// - title: Search by title
    /// - author: Search by author
    /// - sort: Sort order (new, old, rating, random)
    /// - limit: Number of results
    /// - page: Page number
    /// - language: Filter by language (eng, fre, ger, etc.)
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

        /// <summary>
        /// Provider can handle request if q, title, or author is provided
        /// </summary>
        public bool CanHandle(Dictionary<string, string> parameters)
        {
            return parameters.ContainsKey("q") ||
                   parameters.ContainsKey("title") ||
                   parameters.ContainsKey("author");
        }

        public async Task<ApiResponse> FetchAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var booksData = await GetBooksDataAsync(parameters, cancellationToken);
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

        private async Task<dynamic?> GetBooksDataAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var queryString = BuildQueryString(parameters);
            var cacheKey = $"{CacheKeyPrefix}{queryString}".ToLowerInvariant();

            // Check cache first
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

            // Cache the books data
            await cacheService.SetAsync(cacheKey, booksData, cacheExpiration, cancellationToken);
            logger.LogDebug("Cached books data for {CacheKey}", cacheKey);

            return booksData;
        }

        private string BuildQueryString(Dictionary<string, string> parameters)
        {
            var queryBuilder = new StringBuilder();
            var apiParams = configuration.GetSection($"{ConfigKey}:Parameters").Get<string[]>() ?? [];

            foreach (var param in apiParams)
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