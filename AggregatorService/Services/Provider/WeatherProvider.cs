using AggregatorService.Models.Responses;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Provider.Base;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AggregatorService.Services.Providers
{
    /// <summary>
    /// Provider for OpenWeatherMap API
    /// Supports parameters: city, country (optional), lat, lon, units (optional), lang (optional)
    /// </summary>
    public class WeatherProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cacheService,
        ILogger<WeatherProvider> logger) : IExternalApiProvider
    {
        private const string GeocodingCacheKeyPrefix = "geo:";
        private static readonly TimeSpan GeocodingCacheExpiration = TimeSpan.FromDays(30);

        public string Name => "weather";

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
            // If coordinates provided directly, use them
            if (parameters.TryGetValue("lat", out var lat) && parameters.TryGetValue("lon", out var lon))
            {
                return (lat, lon);
            }

            // Otherwise geocode the city
            if (parameters.TryGetValue("city", out var city))
            {
                var country = parameters.GetValueOrDefault("country", string.Empty);
                return await GeocodeAsync(city, country, cancellationToken);
            }

            return (null, null);
        }

        /// <summary>
        /// Converts city name to coordinates using OpenWeatherMap Geocoding API
        /// Results are cached to reduce API calls
        /// </summary>
        private async Task<(string? lat, string? lon)> GeocodeAsync(
            string city,
            string country,
            CancellationToken cancellationToken)
        {
            var cacheKey = $"{GeocodingCacheKeyPrefix}{city}:{country}".ToLowerInvariant();

            // Check cache first
            var cached = await cacheService.GetAsync<GeocodingResult>(cacheKey, cancellationToken);
            if (cached != null)
            {
                logger.LogDebug("Geocoding cache hit for {City}", city);
                return (cached.Lat, cached.Lon);
            }

            // Call Geocoding API
            var apiKey = configuration["ExternalApis:OpenWeatherMap:ApiKey"];
            var baseUrl = configuration["ExternalApis:OpenWeatherMap:GeocodingUrl"];

            var query = string.IsNullOrEmpty(country) ? city : $"{city},{country}";
            var url = $"{baseUrl}?q={Uri.EscapeDataString(query)}&limit=1&appid={apiKey}";

            var client = httpClientFactory.CreateClient("OpenWeatherMap");
            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<List<GeocodingApiResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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

            // Cache the result
            await cacheService.SetAsync(cacheKey, geocodingResult, GeocodingCacheExpiration, cancellationToken);
            logger.LogDebug("Cached geocoding result for {City}", city);

            return (geocodingResult.Lat, geocodingResult.Lon);
        }

        /// <summary>
        /// Fetches current weather data from OpenWeatherMap API
        /// </summary>
        private async Task<dynamic?> GetWeatherDataAsync(
            string lat,
            string lon,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var apiKey = configuration["ExternalApis:OpenWeatherMap:ApiKey"];
            var baseUrl = configuration["ExternalApis:OpenWeatherMap:WeatherUrl"];

            var urlBuilder = new StringBuilder();
            urlBuilder.Append(baseUrl);
            urlBuilder.Append($"?lat={lat}&lon={lon}&appid={apiKey}");

            // Add optional parameters
            if (parameters.TryGetValue("units", out var units))
            {
                urlBuilder.Append($"&units={units}");
            }

            if (parameters.TryGetValue("lang", out var lang))
            {
                urlBuilder.Append($"&lang={lang}");
            }

            var client = httpClientFactory.CreateClient("OpenWeatherMap");
            var response = await client.GetAsync(urlBuilder.ToString(), cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<dynamic>(content);
        }

        /// <summary>
        /// Internal class for caching geocoding results
        /// </summary>
        private class GeocodingResult
        {
            public string Lat { get; set; } = string.Empty;
            public string Lon { get; set; } = string.Empty;
        }

        /// <summary>
        /// Maps to OpenWeatherMap Geocoding API response
        /// </summary>
        private class GeocodingApiResponse
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
        }
    }
}