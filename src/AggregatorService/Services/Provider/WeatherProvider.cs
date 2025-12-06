using AggregatorService.Models.Responses;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Provider.Base;
using AggregatorService.Services.Statistics;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AggregatorService.Services.Providers
{
    /// <summary>
    /// Provider for OpenWeatherMap One Call API 3.0
    /// Supported parameters:
    /// - Geocoding: city (required), state (optional, US only), country (optional, ISO 3166)
    /// - Direct coordinates: lat, lon
    /// - Weather options: exclude, units, lang
    /// </summary>
    public class WeatherProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cacheService,
        IStatisticsService statisticsService,
        ILogger<WeatherProvider> logger) : IExternalApiProvider
    {
        private const string GeocodingCacheKeyPrefix = "geo:";
        private const string WeatherCacheKeyPrefix = "weather:";
        private static readonly TimeSpan GeocodingCacheExpiration = TimeSpan.FromDays(30);
        private static readonly TimeSpan WeatherCacheExpiration = TimeSpan.FromMinutes(10);

        public string Name => "weather";


        // Seriralizer Options for json transformations
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        /// <summary>
        /// Provider can handle request if city OR (lat and lon) are provided
        /// </summary>
        public bool CanHandle(Dictionary<string, string> parameters)
        {
            var hasCity = parameters.ContainsKey("city");
            var hasCoordinates = parameters.ContainsKey("lat") && parameters.ContainsKey("lon");
            return hasCity || hasCoordinates;
        }

        public async Task<ApiResponse> FetchAsync(Dictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var (lat, lon) = await GetCoordinatesAsync(parameters, cancellationToken);

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

                var weatherData = await GetWeatherDataAsync(lat, lon, parameters, cancellationToken);
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching weather data");
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

        /// <summary>
        /// Gets coordinates either from parameters directly or by geocoding city name
        /// </summary>
        private async Task<(string? lat, string? lon)> GetCoordinatesAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            if (parameters.TryGetValue("lat", out var lat) && parameters.TryGetValue("lon", out var lon))
            {
                return (lat, lon);
            }

            if (parameters.ContainsKey("city"))
            {
                return await GeocodeAsync(parameters, cancellationToken);
            }

            return (null, null);
        }

        /// <summary>
        /// Builds the query string for the weather API (excluding API key)
        /// Used both for cache key and URL building
        /// </summary>
        private static string BuildWeatherQueryString(string lat, string lon, Dictionary<string, string> parameters)
        {
            var queryBuilder = new StringBuilder();
            queryBuilder.Append($"lat={lat}");
            queryBuilder.Append($"&lon={lon}");

            if (parameters.TryGetValue("exclude", out var exclude))
            {
                queryBuilder.Append($"&exclude={exclude}");
            }

            if (parameters.TryGetValue("units", out var units))
            {
                queryBuilder.Append($"&units={units}");
            }

            if (parameters.TryGetValue("lang", out var lang))
            {
                queryBuilder.Append($"&lang={lang}");
            }

            return queryBuilder.ToString();
        }

        /// <summary>
        /// Converts city name to coordinates using OpenWeatherMap Geocoding API
        /// Results are cached to reduce API calls
        /// </summary>
        private async Task<(string? lat, string? lon)> GeocodeAsync(
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var city = parameters["city"];
            var state = parameters.GetValueOrDefault("state", string.Empty);
            var country = parameters.GetValueOrDefault("country", string.Empty);

            // Build query string without API key for cache key
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

            var apiKey = configuration["ExternalApis:OpenWeatherMap:ApiKey"];
            var baseUrl = configuration["ExternalApis:OpenWeatherMap:GeocodingUrl"];
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

            await cacheService.SetAsync(cacheKey, geocodingResult, GeocodingCacheExpiration, cancellationToken);
            logger.LogDebug("Cached geocoding result for {City}", city);

            return (geocodingResult.Lat, geocodingResult.Lon);
        }

        /// <summary>
        /// Fetches weather data from OpenWeatherMap One Call API 3.0
        /// Results are cached based on query string (excluding API key)
        /// </summary>
        private async Task<dynamic?> GetWeatherDataAsync(
            string lat,
            string lon,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            // Build query string without API key
            var queryString = BuildWeatherQueryString(lat, lon, parameters);
            var cacheKey = $"{WeatherCacheKeyPrefix}{queryString}".ToLowerInvariant();

            // Check cache first
            var cached = await cacheService.GetAsync<JsonElement>(cacheKey, cancellationToken);
            if (cached.ValueKind != JsonValueKind.Undefined)
            {
                logger.LogDebug("Weather cache hit for {CacheKey}", cacheKey);
                return cached;
            }

            var apiKey = configuration["ExternalApis:OpenWeatherMap:ApiKey"];
            var baseUrl = configuration["ExternalApis:OpenWeatherMap:WeatherUrl"];
            var url = $"{baseUrl}?{queryString}&appid={apiKey}";

            var client = httpClientFactory.CreateClient();
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var weatherData = JsonSerializer.Deserialize<JsonElement>(content);

            // Cache the weather data
            await cacheService.SetAsync(cacheKey, weatherData, WeatherCacheExpiration, cancellationToken);
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