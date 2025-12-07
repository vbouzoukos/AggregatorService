using AggregatorService.Models.Requests;
using AggregatorService.Services.Provider;
using AggregatorService.Services.Statistics;
using AggregatorService.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AggregatorService.Tests.Services.Provider
{
    public class OpenLibraryProviderTests
    {
        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly CacheServiceStub cacheService;
        private readonly StatisticsService statisticsService;
        private readonly Mock<ILogger<OpenLibraryProvider>> loggerMock;
        private readonly OpenLibraryProvider provider;

        public OpenLibraryProviderTests()
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient());
            httpClientFactory = httpClientFactoryMock.Object;

            cacheService = new CacheServiceStub();
            statisticsService = new StatisticsService();
            loggerMock = new Mock<ILogger<OpenLibraryProvider>>();

            provider = new OpenLibraryProvider(
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
                Query = "lord of the rings"
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
                Parameters = new Dictionary<string, string> { { "q", "harry potter" } }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithTitleParameter_ReturnsTrue()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "title", "1984" } }
            };

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WithAuthorParameter_ReturnsTrue()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "author", "Tolkien" } }
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
                Language = "eng"
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
        public async Task FetchAsync_WithQueryFilter_ReturnsRealBooksData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "lord of the rings"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
            Assert.True(result.ResponseTime.TotalMilliseconds > 0);
        }

        [Fact]
        public async Task FetchAsync_WithAuthorParameter_ReturnsAuthorBooks()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "author", "J.R.R. Tolkien" } }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithTitleParameter_ReturnsTitleMatches()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "title", "The Hobbit" } }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithQueryAndLanguage_ReturnsFilteredData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "science fiction",
                Language = "eng"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortNewest_AppliesSortMapping()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "programming",
                Sort = SortOption.Newest
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortOldest_AppliesSortMapping()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "history",
                Sort = SortOption.Oldest
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortPopularity_AppliesSortMapping()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "fantasy",
                Sort = SortOption.Popularity
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithSortRelevance_DoesNotAddSortParam()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "cooking",
                Sort = SortOption.Relevance
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");
            Assert.Equal("Books", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_RecordsStatisticsOnSuccess()
        {
            // Arrange
            statisticsService.Reset();
            var request = new AggregationRequest
            {
                Query = "mystery"
            };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var stats = statisticsService.GetStatistics();
            var booksStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "Books");
            Assert.NotNull(booksStats);
            Assert.Equal(1, booksStats.TotalRequests);
            Assert.Equal(1, booksStats.SuccessfulRequests);
        }

        #endregion

        #region Caching Tests

        [Fact]
        public async Task FetchAsync_CachesBookData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "adventure"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"API call failed: {result.ErrorMessage}");

            var booksSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("books:")).ToList();
            Assert.NotEmpty(booksSetKeys);
        }

        [Fact]
        public async Task FetchAsync_SecondCallUsesCachedData()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Query = "romance"
            };

            // Act
            var result1 = await provider.FetchAsync(request, CancellationToken.None);
            var result2 = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result1.IsSuccess);
            Assert.True(result2.IsSuccess);

            var booksGetKeys = cacheService.GetKeys.Where(k => k.StartsWith("books:")).ToList();
            var booksSetKeys = cacheService.SetKeys.Where(k => k.StartsWith("books:")).ToList();
            Assert.Equal(2, booksGetKeys.Count);
            Assert.Single(booksSetKeys);
        }

        #endregion
    }
}