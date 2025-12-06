using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Aggregation;
using AggregatorService.Services.Provider.Base;
using Microsoft.Extensions.Logging;
using Moq;

namespace AggregatorService.Tests.Services
{
    public class AggregationServiceTests
    {
        private readonly Mock<ILogger<AggregationService>> _loggerMock;

        public AggregationServiceTests()
        {
            _loggerMock = new Mock<ILogger<AggregationService>>();
        }

        #region No Providers Tests

        [Fact]
        public async Task AggregateAsync_WithNoProviders_ReturnsEmptyResults()
        {
            // Arrange
            var providers = Enumerable.Empty<IExternalApiProvider>();
            var service = new AggregationService(providers, _loggerMock.Object);
            var request = new AggregationRequest();

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(0, result.ProvidersQueried);
            Assert.Equal(0, result.SuccessfulResponses);
            Assert.Empty(result.Results);
        }

        [Fact]
        public async Task AggregateAsync_WithNoProviders_SetsTimestamp()
        {
            // Arrange
            var providers = Enumerable.Empty<IExternalApiProvider>();
            var service = new AggregationService(providers, _loggerMock.Object);
            var request = new AggregationRequest();
            var beforeCall = DateTime.UtcNow;

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.Timestamp >= beforeCall);
            Assert.True(result.Timestamp <= DateTime.UtcNow);
        }

        #endregion

        #region Single Provider Tests

        [Fact]
        public async Task AggregateAsync_WithMatchingProvider_CallsProvider()
        {
            // Arrange
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.Name).Returns("TestProvider");
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            providerMock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse { Provider = "TestProvider", IsSuccess = true });

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(1, result.ProvidersQueried);
            Assert.Equal(1, result.SuccessfulResponses);
            providerMock.Verify(p => p.FetchAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AggregateAsync_WithNonMatchingProvider_SkipsProvider()
        {
            // Arrange
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(false);

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest();

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(0, result.ProvidersQueried);
            providerMock.Verify(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AggregateAsync_WithSuccessfulProvider_ReturnsProviderData()
        {
            // Arrange
            var expectedData = new { temperature = 25, city = "London" };
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.Name).Returns("Weather");
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            providerMock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse
                {
                    Provider = "Weather",
                    IsSuccess = true,
                    Data = expectedData,
                    ResponseTime = TimeSpan.FromMilliseconds(100)
                });

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Parameters = new Dictionary<string, string> { { "city", "London" } } };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Single(result.Results);
            Assert.Equal("Weather", result.Results[0].Provider);
            Assert.True(result.Results[0].IsSuccess);
            Assert.NotNull(result.Results[0].Data);
        }

        #endregion

        #region Multiple Providers Tests

        [Fact]
        public async Task AggregateAsync_WithMultipleProviders_CallsAllMatchingProviders()
        {
            // Arrange
            var provider1Mock = new Mock<IExternalApiProvider>();
            provider1Mock.Setup(p => p.Name).Returns("Provider1");
            provider1Mock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            provider1Mock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse { Provider = "Provider1", IsSuccess = true });

            var provider2Mock = new Mock<IExternalApiProvider>();
            provider2Mock.Setup(p => p.Name).Returns("Provider2");
            provider2Mock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            provider2Mock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse { Provider = "Provider2", IsSuccess = true });

            var service = new AggregationService([provider1Mock.Object, provider2Mock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(2, result.ProvidersQueried);
            Assert.Equal(2, result.SuccessfulResponses);
            Assert.Equal(2, result.Results.Count);
        }

        [Fact]
        public async Task AggregateAsync_WithMixedMatchingProviders_CallsOnlyMatchingProviders()
        {
            // Arrange
            var matchingProvider = new Mock<IExternalApiProvider>();
            matchingProvider.Setup(p => p.Name).Returns("Matching");
            matchingProvider.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            matchingProvider.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse { Provider = "Matching", IsSuccess = true });

            var nonMatchingProvider = new Mock<IExternalApiProvider>();
            nonMatchingProvider.Setup(p => p.Name).Returns("NonMatching");
            nonMatchingProvider.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(false);

            var service = new AggregationService([matchingProvider.Object, nonMatchingProvider.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(1, result.ProvidersQueried);
            matchingProvider.Verify(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            nonMatchingProvider.Verify(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AggregateAsync_ExecutesProvidersInParallel()
        {
            // Arrange
            var provider1StartTime = DateTime.MinValue;
            var provider2StartTime = DateTime.MinValue;

            var provider1Mock = new Mock<IExternalApiProvider>();
            provider1Mock.Setup(p => p.Name).Returns("Provider1");
            provider1Mock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            provider1Mock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    provider1StartTime = DateTime.UtcNow;
                    await Task.Delay(100);
                    return new ApiResponse { Provider = "Provider1", IsSuccess = true };
                });

            var provider2Mock = new Mock<IExternalApiProvider>();
            provider2Mock.Setup(p => p.Name).Returns("Provider2");
            provider2Mock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            provider2Mock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    provider2StartTime = DateTime.UtcNow;
                    await Task.Delay(100);
                    return new ApiResponse { Provider = "Provider2", IsSuccess = true };
                });

            var service = new AggregationService([provider1Mock.Object, provider2Mock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await service.AggregateAsync(request, CancellationToken.None);
            stopwatch.Stop();

            // Assert - If parallel, total time should be ~100ms, not ~200ms
            Assert.True(stopwatch.ElapsedMilliseconds < 180,
                $"Expected parallel execution (~100ms), but took {stopwatch.ElapsedMilliseconds}ms");

            // Both providers should have started at approximately the same time
            var timeDifference = Math.Abs((provider1StartTime - provider2StartTime).TotalMilliseconds);
            Assert.True(timeDifference < 50,
                $"Providers should start near-simultaneously, but started {timeDifference}ms apart");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task AggregateAsync_WhenProviderThrows_ReturnsErrorResponse()
        {
            // Arrange
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.Name).Returns("FailingProvider");
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            providerMock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Connection failed"));

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(1, result.ProvidersQueried);
            Assert.Equal(0, result.SuccessfulResponses);
            Assert.Single(result.Results);
            Assert.False(result.Results[0].IsSuccess);
            Assert.Contains("Connection failed", result.Results[0].ErrorMessage);
        }

        [Fact]
        public async Task AggregateAsync_WithPartialFailure_ReturnsPartialResults()
        {
            // Arrange
            var successProvider = new Mock<IExternalApiProvider>();
            successProvider.Setup(p => p.Name).Returns("SuccessProvider");
            successProvider.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            successProvider.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse { Provider = "SuccessProvider", IsSuccess = true });

            var failProvider = new Mock<IExternalApiProvider>();
            failProvider.Setup(p => p.Name).Returns("FailProvider");
            failProvider.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            failProvider.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API Error"));

            var service = new AggregationService([successProvider.Object, failProvider.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(2, result.ProvidersQueried);
            Assert.Equal(1, result.SuccessfulResponses);
            Assert.Equal(2, result.Results.Count);
        }

        [Fact]
        public async Task AggregateAsync_WhenProviderReturnsFailure_CountsAsUnsuccessful()
        {
            // Arrange
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.Name).Returns("FailingProvider");
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            providerMock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse
                {
                    Provider = "FailingProvider",
                    IsSuccess = false,
                    ErrorMessage = "API returned error"
                });

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.Equal(1, result.ProvidersQueried);
            Assert.Equal(0, result.SuccessfulResponses);
        }

        [Fact]
        public async Task AggregateAsync_ProviderFailureDoesNotAffectOthers()
        {
            // Arrange
            var failFirst = new Mock<IExternalApiProvider>();
            failFirst.Setup(p => p.Name).Returns("FailFirst");
            failFirst.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            failFirst.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("First provider failed"));

            var successSecond = new Mock<IExternalApiProvider>();
            successSecond.Setup(p => p.Name).Returns("SuccessSecond");
            successSecond.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            successSecond.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResponse { Provider = "SuccessSecond", IsSuccess = true, Data = "data" });

            var service = new AggregationService([failFirst.Object, successSecond.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            var successResult = result.Results.FirstOrDefault(r => r.Provider == "SuccessSecond");
            Assert.NotNull(successResult);
            Assert.True(successResult.IsSuccess);
            Assert.NotNull(successResult.Data);
        }

        #endregion

        #region Response Time Tests

        [Fact]
        public async Task AggregateAsync_MeasuresTotalResponseTime()
        {
            // Arrange
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.Name).Returns("SlowProvider");
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            providerMock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(50);
                    return new ApiResponse { Provider = "SlowProvider", IsSuccess = true };
                });

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.TotalResponseTime.TotalMilliseconds >= 50);
        }

        [Fact]
        public async Task AggregateAsync_WithNoMatchingProviders_HasMinimalResponseTime()
        {
            // Arrange
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(false);

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest();

            // Act
            var result = await service.AggregateAsync(request, CancellationToken.None);

            // Assert
            Assert.True(result.TotalResponseTime.TotalMilliseconds < 100);
        }

        #endregion

        #region Cancellation Tests

        [Fact]
        public async Task AggregateAsync_PassesCancellationToken()
        {
            // Arrange
            CancellationToken capturedToken = default;
            var providerMock = new Mock<IExternalApiProvider>();
            providerMock.Setup(p => p.Name).Returns("TestProvider");
            providerMock.Setup(p => p.CanHandle(It.IsAny<AggregationRequest>())).Returns(true);
            providerMock.Setup(p => p.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .Callback<AggregationRequest, CancellationToken>((req, ct) => capturedToken = ct)
                .ReturnsAsync(new ApiResponse { Provider = "TestProvider", IsSuccess = true });

            var service = new AggregationService([providerMock.Object], _loggerMock.Object);
            var request = new AggregationRequest { Query = "test" };
            using var cts = new CancellationTokenSource();

            // Act
            await service.AggregateAsync(request, cts.Token);

            // Assert
            Assert.Equal(cts.Token, capturedToken);
        }

        #endregion
    }
}