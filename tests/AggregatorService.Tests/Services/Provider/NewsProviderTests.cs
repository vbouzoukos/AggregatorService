using AggregatorService.Models.Requests;
using AggregatorService.Services.Caching;
using AggregatorService.Services.Provider;
using AggregatorService.Services.Statistics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AggregatorService.Tests.Services.Provider
{
    public class NewsProviderTests
    {
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly CacheServiceStub cacheService;
        private readonly StatisticsService statisticsService;
        private readonly Mock<ILogger<NewsProvider>> loggerMock;
        private readonly NewsProvider provider;

        public NewsProviderTests()
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Secret.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var apiKey = configuration["ExternalApis:NewsApi:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "API key not configured. Create appsettings.Secret.json with your API key " +
                    "or set environment variable ExternalApis__NewsApi__ApiKey");
            }

            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient());
            httpClientFactory = httpClientFactoryMock.Object;

            cacheService = new CacheServiceStub();
            statisticsService = new StatisticsService();
            loggerMock = new Mock<ILogger<NewsProvider>>();

            provider = new NewsProvider(
                httpClientFactory,
                configuration,
                cacheService,
                statisticsService,
                loggerMock.Object);
        }

        #region CanHandle Tests

        [Fact]
        public void CanHandle_WithQueryFilter_ReturnsTrue()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "technology"
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithQParameter_ReturnsTrue()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "q", "technology" } }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithEmptyQuery_ReturnsFalse()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = ""
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.False(result);
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
        public void CanHandle_WithOnlyLanguageFilter_ReturnsFalse()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Language = "en"
            };

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
                Parameters = new Dictionary<string, string>
                {
                    { "city", "London" },
                    { "country", "GB" }
                }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Integration Tests - Real API Calls

        [Fact]
        public async Task FetchAsync_WithQueryFilter_ReturnsNewsData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "technology"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
            Assert.True(result.ResponseTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task FetchAsync_WithQueryAndLanguage_ReturnsData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "football",
                Language = "en"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortNewest_AppliesSortMapping()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "sports",
                Sort = SortOption.Newest
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortOldest_AppliesSortMapping()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "science",
                Sort = SortOption.Oldest
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortRelevance_AppliesSortMapping()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "business",
                Sort = SortOption.Relevance
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortPopularity_AppliesSortMapping()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "music",
                Sort = SortOption.Popularity
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithDateRange_ReturnsFilteredData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "technology",
                Parameters = new Dictionary<string, string>
                {
                    { "from", DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd") },
                    { "to", DateTime.UtcNow.ToString("yyyy-MM-dd") }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSearchInParameter_ReturnsData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "AI",
                Parameters = new Dictionary<string, string>
                {
                    { "searchIn", "title" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal("News", result.Provider);
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_RecordsStatistics()
        {
            // Arrange
            statisticsService.Reset();
            var request = new AggregationRequest
            {
                Query = "science"
            };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var stats = statisticsService.GetStatistics();
            var newsStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "News");
            Assert.NotNull(newsStats);
            Assert.Equal(1, newsStats.TotalRequests);
        }

        [Fact]
        public async Task FetchAsync_RecordsSuccessStatistics_OnSuccess()
        {
            // Arrange
            statisticsService.Reset();
            var request = new AggregationRequest
            {
                Query = "weather"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            if (result.IsSuccess)
            {
                var stats = statisticsService.GetStatistics();
                var newsStats = stats.Providers.First(p => p.ProviderName == "News");
                Assert.Equal(1, newsStats.SuccessfulRequests);
                Assert.Equal(0, newsStats.FailedRequests);
            }
        }

        #endregion

        #region Caching Tests

        [Fact]
        public async Task FetchAsync_CacheKeyDoesNotContainApiKey()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "business"
            };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert - Cache operations should have been attempted
            var newsKeys = cacheService.GetKeys.Where(k => k.StartsWith("news:")).ToList();
            Assert.NotEmpty(newsKeys);
            Assert.All(newsKeys, key => Assert.DoesNotContain("apikey", key.ToLowerInvariant()));
        }

        [Fact]
        public async Task FetchAsync_AttemptsCacheOperations()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "health"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert - Should always attempt cache get
            var newsGetKeys = cacheService.GetKeys.Where(k => k.StartsWith("news:")).ToList();
            Assert.NotEmpty(newsGetKeys);

            // If successful, should also set cache
            if (result.IsSuccess)
            {
                var newsSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("news:")).ToList();
                Assert.NotEmpty(newsSetKeys);
            }
        }

        [Fact]
        public async Task FetchAsync_SecondCallUsesCachedData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "entertainment"
            };

            // Act
            var result1 = await provider.FetchAsync(request, CancellationToken.None);

            // Only proceed with cache test if first call succeeded
            Assert.True(result1.IsSuccess, $"First API call failed: {result1.ErrorMessage}");

            var result2 = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result2.IsSuccess);

            // Should have 2 get attempts but only 1 set (second call hits cache)
            var newsGetKeys = cacheService.GetKeys.Where(k => k.StartsWith("news:")).ToList();
            var newsSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("news:")).ToList();
            Assert.Equal(2, newsGetKeys.Count);
            Assert.Single(newsSetKeys);
        }

        [Fact]
        public async Task FetchAsync_DifferentQueries_CreateDifferentCacheKeys()
        {
            // Arrange
            var request1 = new AggregationRequest { Query = "technology" };
            var request2 = new AggregationRequest { Query = "sports" };

            // Act
            var result1 = await provider.FetchAsync(request1, CancellationToken.None);
            var result2 = await provider.FetchAsync(request2, CancellationToken.None);

            // Assert
            Assert.True(result1.IsSuccess, $"First API call failed: {result1.ErrorMessage}");
            Assert.True(result2.IsSuccess, $"Second API call failed: {result2.ErrorMessage}");

            var newsSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("news:")).ToList();
            Assert.Equal(2, newsSetKeys.Count);
            Assert.NotEqual(newsSetKeys[0], newsSetKeys[1]);
        }

        #endregion
    }
}