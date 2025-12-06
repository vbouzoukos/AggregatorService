using AggregatorService.Models.Responses;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Providers;
using AggregatorService.Services.Statistics;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AggregatorService.Services.Provider
{
    public class NewsProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cacheService,
        IStatisticsService statisticsService,
        ILogger<NewsProvider> logger) : IExternalApiProvider
    {
        private const string cacheNewsKey = "news:";
        private const string configKey = "ExternalApis:NewsApi";
        public string Name => "News";

        private readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(configuration.GetValue<int>($"{configKey}:CacheMinutes"));
        public bool CanHandle(Dictionary<string, string> parameters)
        {
            return parameters.ContainsKey("q");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<ApiResponse> FetchAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                parameters.TryGetValue("q", out var q);
                if (string.IsNullOrEmpty(q))
                {
                    throw new ArgumentException("q parameter cannot be empty");
                }
                var newsData = await GetNewsDataAsync(q, parameters, cancellationToken);
                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, true);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = true,
                    Data = newsData,
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
        private async Task<dynamic?> GetNewsDataAsync(
            string q,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            // Build query string without API key
            var queryString = BuildQueryString(q, parameters);
            var cacheKey = $"{cacheNewsKey}{queryString}".ToLowerInvariant();

            // Check cache first
            var cached = await cacheService.GetAsync<JsonElement>(cacheKey, cancellationToken);
            if (cached.ValueKind != JsonValueKind.Undefined)
            {
                logger.LogDebug("News cache hit for {CacheKey}", cacheKey);
                return cached;
            }

            var apiKey = configuration[$"{configKey}:ApiKey"];
            var baseUrl = configuration[$"{configKey}:Url"];
            var url = $"{baseUrl}?{queryString}&apiKey={apiKey}";

            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var weatherData = JsonSerializer.Deserialize<JsonElement>(content);

            // Cache the weather data
            await cacheService.SetAsync(cacheKey, weatherData, cacheExpiration, cancellationToken);
            logger.LogDebug("Cached weather data for {CacheKey}", cacheKey);

            return weatherData;
        }

        private string BuildQueryString(string q, Dictionary<string, string> parameters)
        {
            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"q={q}");
            string[] apiParams = configuration.GetSection($"{configKey}:parameters").Get<string[]>()!;

            foreach (var param in apiParams)
            {
                if (parameters.TryGetValue(param, out var paramValue))
                {
                    queryBuilder.Append($"&{param}={Uri.EscapeDataString(paramValue)}");
                }
            }

            return queryBuilder.ToString();
        }

    }
}
