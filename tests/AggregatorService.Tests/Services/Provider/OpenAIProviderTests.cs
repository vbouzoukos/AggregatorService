using AggregatorService.Models.Requests;
using AggregatorService.Services.Provider;
using AggregatorService.Services.Statistics;
using AggregatorService.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace AggregatorService.Tests.Services.Provider
{
    public class OpenAIProviderTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly CacheServiceStub _cacheService;
        private readonly StatisticsService _statisticsService;
        private readonly Mock<ILogger<OpenAIProvider>> _loggerMock;

        public OpenAIProviderTests()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _cacheService = new CacheServiceStub();
            _statisticsService = new StatisticsService();
            _loggerMock = new Mock<ILogger<OpenAIProvider>>();
        }

        private OpenAIProvider CreateProvider(bool includeApiKey = true)
        {
            var client = new HttpClient(_httpMessageHandlerMock.Object);
            _httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(client);

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Secret.json", optional: true);

            if (!includeApiKey)
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ExternalApis:OpenAI:ApiKey", null }
                });
            }

            var configuration = configBuilder.Build();

            return new OpenAIProvider(
                _httpClientFactoryMock.Object,
                configuration,
                _cacheService,
                _statisticsService,
                _loggerMock.Object);
        }

        private void SetupHttpResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content)
                });
        }

        private static string CreateOpenAIResponse(string promptContent)
        {
            return JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            role = "assistant",
                            content = promptContent
                        }
                    }
                }
            });
        }

        #region CanHandle Tests

        [Fact]
        public void CanHandle_WhenApiKeyConfigured_ReturnsTrue()
        {
            // Arrange
            var provider = CreateProvider(includeApiKey: true);
            var request = new AggregationRequest();

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanHandle_WhenApiKeyNotConfigured_ReturnsFalse()
        {
            // Arrange
            var provider = CreateProvider(includeApiKey: false);
            var request = new AggregationRequest();

            // Act
            var result = provider.CanHandle(request);

            // Assert
            Assert.False(result);
        }
        #endregion

        #region FetchAsync Tests

        [Fact]
        public async Task FetchAsync_WithValidRequest_ReturnsPrompt()
        {
            // Arrange
            var provider = CreateProvider();
            var expectedPrompt = "Analyze the following data for insights about artificial intelligence...";
            SetupHttpResponse(CreateOpenAIResponse(expectedPrompt));

            var request = new AggregationRequest
            {
                Query = "artificial intelligence",
                Language = "en"
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"Expected success but got error: {result.ErrorMessage}");
            Assert.Equal("AIPrompt", result.Provider);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task FetchAsync_WithAllParameters_ReturnsPrompt()
        {
            // Arrange
            var provider = CreateProvider();
            var expectedPrompt = "Comprehensive analysis prompt...";
            SetupHttpResponse(CreateOpenAIResponse(expectedPrompt));

            var request = new AggregationRequest
            {
                Query = "technology",
                Language = "en",
                Country = "GB",
                Sort = SortOption.Newest,
                Parameters = new Dictionary<string, string>
                {
                    { "city", "London" },
                    { "author", "Asimov" }
                }
            };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess, $"Expected success but got error: {result.ErrorMessage}");
            Assert.Equal("AIPrompt", result.Provider);
        }

        [Fact]
        public async Task FetchAsync_WhenApiError_ReturnsErrorResponse()
        {
            // Arrange
            var provider = CreateProvider();
            SetupHttpResponse("Internal Server Error", HttpStatusCode.InternalServerError);

            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("AIPrompt", result.Provider);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task FetchAsync_WhenEmptyChoices_ReturnsErrorResponse()
        {
            // Arrange
            var provider = CreateProvider();
            var emptyResponse = JsonSerializer.Serialize(new { choices = Array.Empty<object>() });
            SetupHttpResponse(emptyResponse);

            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.ErrorMessage);
        }

        #endregion

        #region Caching Tests

        [Fact]
        public async Task FetchAsync_CachesResponse()
        {
            // Arrange
            var provider = CreateProvider();
            var expectedPrompt = "Cached prompt...";
            SetupHttpResponse(CreateOpenAIResponse(expectedPrompt));

            var request = new AggregationRequest { Query = "caching test" };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var cacheKeys = _cacheService.SetKeys.Where(k => k.StartsWith("openai:")).ToList();
            Assert.NotEmpty(cacheKeys);
        }

        [Fact]
        public async Task FetchAsync_SecondCallUsesCachedData()
        {
            // Arrange
            var provider = CreateProvider();
            var expectedPrompt = "Cached prompt for second call...";
            SetupHttpResponse(CreateOpenAIResponse(expectedPrompt));

            var request = new AggregationRequest { Query = "cache hit test" };

            // Act
            var result1 = await provider.FetchAsync(request, CancellationToken.None);
            var result2 = await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result1.IsSuccess);
            Assert.True(result2.IsSuccess);

            var getKeys = _cacheService.GetKeys.Where(k => k.StartsWith("openai:")).ToList();
            var setKeys = _cacheService.SetKeys.Where(k => k.StartsWith("openai:")).ToList();

            Assert.Equal(2, getKeys.Count); // Two get attempts
            Assert.Single(setKeys); // Only one set (first call)
        }

        [Fact]
        public async Task FetchAsync_DifferentRequests_CreateDifferentCacheKeys()
        {
            // Arrange
            var provider = CreateProvider();
            SetupHttpResponse(CreateOpenAIResponse("Prompt 1"));

            var request1 = new AggregationRequest { Query = "technology" };
            var request2 = new AggregationRequest { Query = "science" };

            // Act
            await provider.FetchAsync(request1, CancellationToken.None);

            SetupHttpResponse(CreateOpenAIResponse("Prompt 2"));
            await provider.FetchAsync(request2, CancellationToken.None);

            // Assert
            var setKeys = _cacheService.SetKeys.Where(k => k.StartsWith("openai:")).ToList();
            Assert.Equal(2, setKeys.Count);
            Assert.NotEqual(setKeys[0], setKeys[1]);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public async Task FetchAsync_RecordsStatistics_OnSuccess()
        {
            // Arrange
            _statisticsService.Reset();
            var provider = CreateProvider();
            SetupHttpResponse(CreateOpenAIResponse("Test prompt"));

            var request = new AggregationRequest { Query = "stats test" };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var stats = _statisticsService.GetStatistics();
            var aiStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "AIPrompt");
            Assert.NotNull(aiStats);
            Assert.Equal(1, aiStats.TotalRequests);
            Assert.Equal(1, aiStats.SuccessfulRequests);
        }

        [Fact]
        public async Task FetchAsync_RecordsStatistics_OnFailure()
        {
            // Arrange
            _statisticsService.Reset();
            var provider = CreateProvider();
            SetupHttpResponse("Error", HttpStatusCode.BadRequest);

            var request = new AggregationRequest { Query = "failure test" };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var stats = _statisticsService.GetStatistics();
            var aiStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "AIPrompt");
            Assert.NotNull(aiStats);
            Assert.Equal(1, aiStats.TotalRequests);
            Assert.Equal(1, aiStats.FailedRequests);
        }

        #endregion

        #region Cache Key Tests

        [Fact]
        public async Task CacheKey_IncludesQuery()
        {
            // Arrange
            var provider = CreateProvider();
            SetupHttpResponse(CreateOpenAIResponse("Test"));

            var request = new AggregationRequest { Query = "testquery" };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var cacheKey = _cacheService.SetKeys.First(k => k.StartsWith("openai:"));
            Assert.Contains("q=testquery", cacheKey);
        }

        [Fact]
        public async Task CacheKey_IncludesAllParameters()
        {
            // Arrange
            var provider = CreateProvider();
            SetupHttpResponse(CreateOpenAIResponse("Test"));

            var request = new AggregationRequest
            {
                Query = "test",
                Language = "en",
                Country = "US",
                Sort = SortOption.Newest,
                Parameters = new Dictionary<string, string> { { "city", "NYC" } }
            };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var cacheKey = _cacheService.SetKeys.First(k => k.StartsWith("openai:"));
            Assert.Contains("q=test", cacheKey);
            Assert.Contains("lang=en", cacheKey);
            Assert.Contains("country=us", cacheKey);
            Assert.Contains("sort=newest", cacheKey);
            Assert.Contains("city=nyc", cacheKey);
        }

        [Fact]
        public async Task CacheKey_DoesNotContainApiKey()
        {
            // Arrange
            var provider = CreateProvider();
            SetupHttpResponse(CreateOpenAIResponse("Test"));

            var request = new AggregationRequest { Query = "security test" };

            // Act
            await provider.FetchAsync(request, CancellationToken.None);

            // Assert
            var cacheKey = _cacheService.SetKeys.First(k => k.StartsWith("openai:"));
            Assert.DoesNotContain("apikey", cacheKey.ToLowerInvariant());
        }

        #endregion
    }
}