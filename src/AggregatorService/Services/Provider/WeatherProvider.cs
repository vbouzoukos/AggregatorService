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
    /// Provider for OpenWeatherMap API
    /// Filters, sort mappings, and required parameters configured in appsettings.json
    /// </summary>
    public class WeatherProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cacheService,
        IStatisticsService statisticsService,
        ILogger<WeatherProvider> logger) : IExternalApiProvider
    {
        private const string ConfigKey = "ExternalApis:OpenWeatherMap";
        private const string GeocodingCacheKeyPrefix = "geo:";
        private const string WeatherCacheKeyPrefix = "weather:";

        private readonly TimeSpan geocodingCacheExpiration = TimeSpan.FromDays(
            configuration.GetValue($"{ConfigKey}:CacheGeoDays", 30));
        private readonly TimeSpan weatherCacheExpiration = TimeSpan.FromMinutes(
            configuration.GetValue($"{ConfigKey}:CacheDataMinutes", 10));

        public string Name => "Weather";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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

                var (lat, lon) = await GetCoordinatesAsync(workingParams, cancellationToken);

                if (lat == null || lon == null)
                {
                    stopwatch.Stop();
                    statisticsService.RecordRequest(Name, stopwatch.Elapsed, false);

                    return new ApiResponse
                    {
                        Provider = Name,
                        IsSuccess = false,
                        ErrorMessage = "Could not resolve coordinates for the provided location",
                        ResponseTime = stopwatch.Elapsed
                    };
                }

                var weatherData = await GetWeatherDataAsync(lat, lon, workingParams, cancellationToken);
                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, true);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = true,
                    Data = weatherData,
                    ResponseTime = stopwatch.Elapsed
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Respect cancellation - rethrow to let caller handle
                throw;
            }
            catch (HttpRequestException ex)
            {
                var statusInfo = ex.StatusCode.HasValue ? $" (HTTP {(int)ex.StatusCode})" : "";
                logger.LogWarning(ex, "HTTP error fetching {Name} data Code:{StatusInfo}", Name, statusInfo);
                stopwatch.Stop();
                statisticsService.RecordRequest(Name, stopwatch.Elapsed, false);

                return new ApiResponse
                {
                    Provider = Name,
                    IsSuccess = false,
                    ErrorMessage = $"HTTP error{statusInfo}: {ex.Message}",
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

        private async Task<(string? lat, string? lon)> GetCoordinatesAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            if (parameters.TryGetValue("lat", out var lat) && parameters.TryGetValue("lon", out var lon))
            {
                return (lat, lon);
            }

            if (parameters.TryGetValue("city", out var city) && !string.IsNullOrEmpty(city))
            {
                return await GeocodeAsync(parameters, cancellationToken);
            }

            return (null, null);
        }

        private string BuildWeatherQueryString(string lat, string lon, Dictionary<string, string> parameters)
        {
            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"lat={lat}");
            queryBuilder.Append($"&lon={lon}");

            // Get all parameter keys: filters + configured parameters
            var filters = configuration.GetSection($"{ConfigKey}:Filters").Get<Dictionary<string, string>>() ?? [];
            var apiParams = configuration.GetSection($"{ConfigKey}:Parameters").Get<string[]>() ?? [];

            // Combine filter values and api params
            var allParams = filters.Values.Concat(apiParams).Distinct();

            foreach (var param in allParams)
            {
                if (parameters.TryGetValue(param, out var paramValue) && !string.IsNullOrEmpty(paramValue))
                {
                    queryBuilder.Append($"&{param}={Uri.EscapeDataString(paramValue)}");
                }
            }

            return queryBuilder.ToString();
        }

        private async Task<(string? lat, string? lon)> GeocodeAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var city = parameters["city"];
            var state = parameters.GetValueOrDefault("state", string.Empty);
            var country = parameters.GetValueOrDefault("country", string.Empty);

            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"q={Uri.EscapeDataString(city)}");
            if (!string.IsNullOrEmpty(state))
            {
                queryBuilder.Append($",{Uri.EscapeDataString(state)}");
            }
            if (!string.IsNullOrEmpty(country))
            {
                if (string.IsNullOrEmpty(state))
                {
                    queryBuilder.Append(',');
                }
                queryBuilder.Append($",{Uri.EscapeDataString(country)}");
            }
            queryBuilder.Append("&limit=1");

            var cacheKey = $"{GeocodingCacheKeyPrefix}{queryBuilder}".ToLowerInvariant();

            var cached = await cacheService.GetAsync<GeocodingResult>(cacheKey, cancellationToken);
            if (cached != null)
            {
                logger.LogDebug("Geocoding cache hit for {City}", city);
                return (cached.Lat, cached.Lon);
            }

            var apiKey = configuration[$"{ConfigKey}:ApiKey"];
            var baseUrl = configuration[$"{ConfigKey}:GeocodingUrl"];
            var url = $"{baseUrl}?{queryBuilder}&appid={apiKey}";

            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<List<GeocodingApiResponse>>(content, JsonOptions);

            if (results == null || results.Count == 0)
            {
                logger.LogWarning("No geocoding results found for {City}", city);
                return (null, null);
            }

            var result = results[0];
            var geocodingResult = new GeocodingResult
            {
                Lat = result.Lat.ToString(),
                Lon = result.Lon.ToString()
            };

            await cacheService.SetAsync(cacheKey, geocodingResult, geocodingCacheExpiration, cancellationToken);
            logger.LogDebug("Cached geocoding result for {City}", city);

            return (geocodingResult.Lat, geocodingResult.Lon);
        }

        private async Task<dynamic?> GetWeatherDataAsync(
            string lat,
            string lon,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var queryString = BuildWeatherQueryString(lat, lon, parameters);
            var cacheKey = $"{WeatherCacheKeyPrefix}{queryString}".ToLowerInvariant();

            var cached = await cacheService.GetAsync<JsonElement>(cacheKey, cancellationToken);
            if (cached.ValueKind != JsonValueKind.Undefined)
            {
                logger.LogDebug("Weather cache hit for {CacheKey}", cacheKey);
                return cached;
            }

            var apiKey = configuration[$"{ConfigKey}:ApiKey"];
            var baseUrl = configuration[$"{ConfigKey}:WeatherUrl"];
            var url = $"{baseUrl}?{queryString}&appid={apiKey}";

            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var weatherData = JsonSerializer.Deserialize<JsonElement>(content);

            await cacheService.SetAsync(cacheKey, weatherData, weatherCacheExpiration, cancellationToken);
            logger.LogDebug("Cached weather data for {CacheKey}", cacheKey);

            return weatherData;
        }

        private sealed class GeocodingResult
        {
            public string Lat { get; set; } = string.Empty;
            public string Lon { get; set; } = string.Empty;
        }

        private sealed class GeocodingApiResponse
        {
            public double Lat { get; set; } = 0.0;
            public double Lon { get; set; } = 0.0;
            public string Name { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
        }
    }
}