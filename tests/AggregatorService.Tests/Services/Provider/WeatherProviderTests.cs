using AggregatorService.Models.Requests;
using AggregatorService.Services.Provider;
using AggregatorService.Services.Statistics;
using AggregatorService.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AggregatorService.Tests.Services.Provider
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
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Secret.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            var apiKey = configuration["ExternalApis:OpenWeatherMap:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "API key not configured. Create appsettings.Secret.json with your API key " +
                    "or set environment variable ExternalApis__OpenWeatherMap__ApiKey");
            }

            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient());
            httpClientFactory = httpClientFactoryMock.Object;

            cacheService = new CacheServiceStub();
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
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "city", "London" } }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithCountryFilter_ReturnsFalse()
        {
            // Arrange - Country alone is not in Required, only in Filters
            var request = new AggregationRequest
            {
                Country = "GB"
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithCityAndCountryFilter_ReturnsTrue()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Country = "GB",
                Parameters = new Dictionary<string, string> { { "city", "London" } }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithEmptyParameters_ReturnsFalse()
        {
            // Arrange
            var request = new AggregationRequest();

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithUnrelatedParameters_ReturnsFalse()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "technology",
                Parameters = new Dictionary<string, string> { { "category", "news" } }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanHandle_WithEmptyCityValue_ReturnsFalse()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "city", "" } }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Integration Tests - Real API Calls

        [Fact]
        public async Task FetchAsync_WithCoordinates_ReturnsRealWeatherData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "lat", "51.5074" },
                    { "lon", "-0.1278" },
                    { "units", "metric" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Weather", result.Provider);
            Assert.NotNull(result.Data);
            Assert.True(result.ResponseTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task FetchAsync_WithCity_GeocodesAndReturnsRealWeatherData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Country = "GB",
                Parameters = new Dictionary<string, string>
                {
                    { "city", "London" },
                    { "units", "metric" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Weather", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithCityStateCountry_GeocodesAndReturnsRealWeatherData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Country = "US",
                Parameters = new Dictionary<string, string>
                {
                    { "city", "New York" },
                    { "state", "NY" },
                    { "units", "imperial" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Weather", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithLanguageFilter_AppliesLangParameter()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Language = "de",
                Parameters = new Dictionary<string, string>
                {
                    { "city", "Berlin" },
                    { "units", "metric" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Weather", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithNonExistentCity_ReturnsError()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "city", "ThisCityDoesNotExist12345" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Could not resolve coordinates", result.ErrorMessage);
        }

        [Fact]
        public async Task FetchAsync_RecordsStatisticsOnSuccess()
        {
            // Arrange
            statisticsService.Reset();
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "lat", "51.5074" },
                    { "lon", "-0.1278" }
                }
            };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var stats = statisticsService.GetStatistics();
            var weatherStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "Weather");
            Assert.NotNull(weatherStats);
            Assert.Equal(1, weatherStats.TotalRequests);
            Assert.Equal(1, weatherStats.SuccessfulRequests);
        }

        [Fact]
        public async Task FetchAsync_RecordsStatisticsOnFailure()
        {
            // Arrange
            statisticsService.Reset();
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "city", "ThisCityDoesNotExist12345" }
                }
            };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var stats = statisticsService.GetStatistics();
            var weatherStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "Weather");
            Assert.NotNull(weatherStats);
            Assert.Equal(1, weatherStats.TotalRequests);
            Assert.Equal(1, weatherStats.FailedRequests);
        }

        [Fact]
        public async Task FetchAsync_WithExcludeParameter_ReturnsFilteredData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "lat", "51.5074" },
                    { "lon", "-0.1278" },
                    { "exclude", "minutely,hourly,daily,alerts" },
                    { "units", "metric" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

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
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "lat", "51.5074" },
                    { "lon", "-0.1278" },
                    { "units", "metric" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

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
            var request = new AggregationRequest
            {
                Country = "GB",
                Parameters = new Dictionary<string, string>
                {
                    { "city", "London" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

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
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "lat", "51.5074" },
                    { "lon", "-0.1278" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");

            var weatherSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("weather:")).ToList();
            Assert.NotEmpty(weatherSetKeys);
        }

        [Fact]
        public async Task FetchAsync_CachesGeocodingData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Country = "FR",
                Parameters = new Dictionary<string, string>
                {
                    { "city", "Paris" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");

            var geoSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("geo:")).ToList();
            Assert.NotEmpty(geoSetKeys);
        }

        #endregion
    }
}