using AggregatorService.Services.Caching;
using AggregatorService.Services.Providers;
using AggregatorService.Services.Statistics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace AggregatorService.Tests.Services.Providers
{
    public class WeatherProviderTests
    {
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly CacheServiceStub cacheService;
        private readonly StatisticsService statisticsService;
        private readonly Mock<ILogger<WeatherProvider>> loggerMock;
        private readonly WeatherProvider provider;

        public WeatherProviderTests()
        {
            // Load configuration securely from secrets file (ignored by git) and environment variables
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Secret.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            // Fail fast if API key is missing
            var apiKey = configuration["ExternalApis:OpenWeatherMap:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "API key not configured. Create appsettings.Secret.json with your API key " +
                    "or set environment variable ExternalApis__OpenWeatherMap__ApiKey");
            }

            // HttpClientFactory returns a new client each time - no need to manage lifecycle
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient());
            httpClientFactory = httpClientFactoryMock.Object;

            // Type-safe cache stub
            cacheService = new CacheServiceStub();

            // Real statistics service
            statisticsService = new StatisticsService();

            loggerMock = new Mock<ILogger<WeatherProvider>>();

            provider = new WeatherProvider(
                httpClientFactory,
                configuration,
                cacheService,
                statisticsService,
                loggerMock.Object);
        }

        #region CanHandle Tests

        [Fact]
        public void CanHandle_WithCity_ReturnsTrue()
        {
            // Arrange
            var parameters = new Dictionary<string, string> { { "city", "London" } };

            // Act
            var result = provider.CanHandle(parameters);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithLatAndLon_ReturnsTrue()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "lat", "51.5074" },
                { "lon", "-0.1278" }
            };

            // Act
            var result = provider.CanHandle(parameters);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithOnlyLat_ReturnsFalse()
        {
            // Arrange
            var parameters = new Dictionary<string, string> { { "lat", "51.5074" } };

            // Act
            var result = provider.CanHandle(parameters);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithEmptyParameters_ReturnsFalse()
        {
            // Arrange
            var parameters = new Dictionary<string, string>();

            // Act
            var result = provider.CanHandle(parameters);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithUnrelatedParameters_ReturnsFalse()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "query", "technology" },
                { "category", "news" }
            };

            // Act
            var result = provider.CanHandle(parameters);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Integration Tests - Real API Calls

        [Fact]
        public async Task FetchAsync_WithCoordinates_ReturnsRealWeatherData()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "lat", "51.5074" },
                { "lon", "-0.1278" },
                { "units", "metric" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("weather", result.Provider);
            Assert.NotNull(result.Data);
            Assert.True(result.ResponseTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task FetchAsync_WithCity_GeocodesAndReturnsRealWeatherData()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "city", "London" },
                { "country", "GB" },
                { "units", "metric" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("weather", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithCityStateCountry_GeocodesAndReturnsRealWeatherData()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "city", "New York" },
                { "state", "NY" },
                { "country", "US" },
                { "units", "imperial" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("weather", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithNonExistentCity_ReturnsError()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "city", "ThisCityDoesNotExist12345" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Could not resolve coordinates", result.ErrorMessage);
        }

        [Fact]
        public async Task FetchAsync_RecordsStatisticsOnSuccess()
        {
            // Arrange
            statisticsService.Reset();
            var parameters = new Dictionary<string, string>
            {
                { "lat", "51.5074" },
                { "lon", "-0.1278" }
            };

            // Act
            await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            var stats = statisticsService.GetStatistics();
            var weatherStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "weather");
            Assert.NotNull(weatherStats);
            Assert.Equal(1, weatherStats.TotalRequests);
            Assert.Equal(1, weatherStats.SuccessfulRequests);
        }

        [Fact]
        public async Task FetchAsync_RecordsStatisticsOnFailure()
        {
            // Arrange
            statisticsService.Reset();
            var parameters = new Dictionary<string, string>
            {
                { "city", "ThisCityDoesNotExist12345" }
            };

            // Act
            await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            var stats = statisticsService.GetStatistics();
            var weatherStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "weather");
            Assert.NotNull(weatherStats);
            Assert.Equal(1, weatherStats.TotalRequests);
            Assert.Equal(1, weatherStats.FailedRequests);
        }

        [Fact]
        public async Task FetchAsync_WithExcludeParameter_ReturnsFilteredData()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "lat", "51.5074" },
                { "lon", "-0.1278" },
                { "exclude", "minutely,hourly,daily,alerts" },
                { "units", "metric" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        #endregion

        #region Caching Tests

        [Fact]
        public async Task FetchAsync_WeatherCacheKeyDoesNotContainApiKey()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "lat", "51.5074" },
                { "lon", "-0.1278" },
                { "units", "metric" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");

            var weatherKeys = cacheService.GetKeys.Where(k => k.StartsWith("weather:")).ToList();
            Assert.NotEmpty(weatherKeys);
            Assert.All(weatherKeys, key => Assert.DoesNotContain("appid", key));
        }

        [Fact]
        public async Task FetchAsync_GeocodingCacheKeyDoesNotContainApiKey()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "city", "London" },
                { "country", "GB" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");

            var geoKeys = cacheService.GetKeys.Where(k => k.StartsWith("geo:")).ToList();
            Assert.NotEmpty(geoKeys);
            Assert.All(geoKeys, key => Assert.DoesNotContain("appid", key));
        }

        [Fact]
        public async Task FetchAsync_CachesWeatherData()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "lat", "51.5074" },
                { "lon", "-0.1278" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");

            var weatherSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("weather:")).ToList();
            Assert.NotEmpty(weatherSetKeys);
        }

        [Fact]
        public async Task FetchAsync_CachesGeocodingData()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                { "city", "Paris" },
                { "country", "FR" }
            };

            // Act
            var result = await provider.FetchAsync(parameters, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");

            var geoSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("geo:")).ToList();
            Assert.NotEmpty(geoSetKeys);
        }

        #endregion
    }

    /// <summary>
    /// Type-safe cache service stub that tracks all cache operations
    /// </summary>
    public class CacheServiceStub : ICacheService
    {
        private readonly Dictionary<string, object?> cache = [];
        private readonly List<string> getKeys = [];
        private readonly List<string> setKeys = [];

        public IReadOnlyList<string> GetKeys => getKeys;
        public IReadOnlyList<string> SetKeys => setKeys;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            getKeys.Add(key);

            if (cache.TryGetValue(key, out var value) && value is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
            }

            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            setKeys.Add(key);
            cache[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}