using AggregatorService.Controllers;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Statistics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace AggregatorService.Tests.Controllers
{
    public class StatisticsControllerTests
    {
        private readonly Mock<IStatisticsService> statisticsServiceMock;
        private readonly IConfiguration configuration;
        private readonly StatisticsController controller;

        public StatisticsControllerTests()
        {
            statisticsServiceMock = new Mock<IStatisticsService>();

            // Setup configuration for performance monitor
            var configValues = new Dictionary<string, string?>
            {
                { "PerformanceMonitor:RecentWindowMinutes", "5" },
                { "PerformanceMonitor:AnomalyThresholdPercent", "50" }
            };

            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            controller = new StatisticsController(statisticsServiceMock.Object, configuration);
        }

        #region GetStatistics Tests

        [Fact]
        public void GetStatistics_ReturnsOkWithResponse()
        {
            // Arrange
            var expectedResponse = new StatisticsResponse
            {
                Timestamp = DateTime.UtcNow,
                Providers =
                [
                    new ProviderStatistics
                    {
                        ProviderName = "Weather",
                        TotalRequests = 100,
                        SuccessfulRequests = 95,
                        FailedRequests = 5,
                        AverageResponseTimeMs = 150.5,
                        PerformanceBuckets = new PerformanceBuckets
                        {
                            Fast = 60,
                            Average = 30,
                            Slow = 10
                        }
                    }
                ]
            };

            statisticsServiceMock
                .Setup(s => s.GetStatistics())
                .Returns(expectedResponse);

            // Act
            var result = controller.GetStatistics();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<StatisticsResponse>(okResult.Value);
            Assert.Single(response.Providers);
            Assert.Equal("Weather", response.Providers[0].ProviderName);
            Assert.Equal(100, response.Providers[0].TotalRequests);
        }

        [Fact]
        public void GetStatistics_WithMultipleProviders_ReturnsAllProviders()
        {
            // Arrange
            var expectedResponse = new StatisticsResponse
            {
                Providers =
                [
                    new ProviderStatistics { ProviderName = "Weather", TotalRequests = 50 },
                    new ProviderStatistics { ProviderName = "News", TotalRequests = 30 },
                    new ProviderStatistics { ProviderName = "Books", TotalRequests = 20 }
                ]
            };

            statisticsServiceMock
                .Setup(s => s.GetStatistics())
                .Returns(expectedResponse);

            // Act
            var result = controller.GetStatistics();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<StatisticsResponse>(okResult.Value);
            Assert.Equal(3, response.Providers.Count);
        }

        [Fact]
        public void GetStatistics_WithNoData_ReturnsEmptyProviders()
        {
            // Arrange
            var expectedResponse = new StatisticsResponse
            {
                Providers = []
            };

            statisticsServiceMock
                .Setup(s => s.GetStatistics())
                .Returns(expectedResponse);

            // Act
            var result = controller.GetStatistics();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<StatisticsResponse>(okResult.Value);
            Assert.Empty(response.Providers);
        }

        [Fact]
        public void GetStatistics_VerifiesPerformanceBuckets()
        {
            // Arrange
            var expectedResponse = new StatisticsResponse
            {
                Providers =
                [
                    new ProviderStatistics
                    {
                        ProviderName = "Weather",
                        TotalRequests = 100,
                        PerformanceBuckets = new PerformanceBuckets
                        {
                            Fast = 50,
                            Average = 35,
                            Slow = 15
                        }
                    }
                ]
            };

            statisticsServiceMock
                .Setup(s => s.GetStatistics())
                .Returns(expectedResponse);

            // Act
            var result = controller.GetStatistics();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<StatisticsResponse>(okResult.Value);
            var buckets = response.Providers[0].PerformanceBuckets;
            Assert.Equal(50, buckets.Fast);
            Assert.Equal(35, buckets.Average);
            Assert.Equal(15, buckets.Slow);
            Assert.Equal(100, buckets.Fast + buckets.Average + buckets.Slow);
        }

        #endregion

        #region GetPerformanceStatus Tests

        [Fact]
        public void GetPerformanceStatus_ReturnsOkWithResponse()
        {
            // Arrange
            statisticsServiceMock
                .Setup(s => s.GetProviderNames())
                .Returns(["Weather", "News"]);

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 200,
                    OverallRequestCount = 100,
                    RecentAverageMs = 220,
                    RecentRequestCount = 10
                });

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("News", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "News",
                    OverallAverageMs = 300,
                    OverallRequestCount = 50,
                    RecentAverageMs = 310,
                    RecentRequestCount = 5
                });

            // Act
            var result = controller.GetPerformanceStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PerformanceAnomalyResponse>(okResult.Value);
            Assert.Equal(5, response.RecentWindowMinutes);
            Assert.Equal(50, response.AnomalyThresholdPercent);
            Assert.Equal(2, response.Providers.Count);
        }

        [Fact]
        public void GetPerformanceStatus_DetectsAnomaly_WhenDegradationExceedsThreshold()
        {
            // Arrange
            statisticsServiceMock
                .Setup(s => s.GetProviderNames())
                .Returns(["Weather"]);

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 200,
                    OverallRequestCount = 100,
                    RecentAverageMs = 350, // 75% degradation
                    RecentRequestCount = 10
                });

            // Act
            var result = controller.GetPerformanceStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PerformanceAnomalyResponse>(okResult.Value);
            var provider = response.Providers[0];
            Assert.True(provider.IsAnomaly);
            Assert.Equal("Anomaly", provider.Status);
            Assert.Equal(75, provider.DegradationPercent);
        }

        [Fact]
        public void GetPerformanceStatus_NoAnomaly_WhenDegradationBelowThreshold()
        {
            // Arrange
            statisticsServiceMock
                .Setup(s => s.GetProviderNames())
                .Returns(["Weather"]);

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 200,
                    OverallRequestCount = 100,
                    RecentAverageMs = 220, // 10% degradation
                    RecentRequestCount = 10
                });

            // Act
            var result = controller.GetPerformanceStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PerformanceAnomalyResponse>(okResult.Value);
            var provider = response.Providers[0];
            Assert.False(provider.IsAnomaly);
            Assert.Equal("Normal", provider.Status);
        }

        [Fact]
        public void GetPerformanceStatus_InsufficientData_WhenNotEnoughRequests()
        {
            // Arrange
            statisticsServiceMock
                .Setup(s => s.GetProviderNames())
                .Returns(["Weather"]);

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 200,
                    OverallRequestCount = 3, // Less than 5
                    RecentAverageMs = 400,
                    RecentRequestCount = 1 // Less than 2
                });

            // Act
            var result = controller.GetPerformanceStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PerformanceAnomalyResponse>(okResult.Value);
            var provider = response.Providers[0];
            Assert.False(provider.IsAnomaly);
            Assert.Equal("Insufficient Data", provider.Status);
        }

        [Fact]
        public void GetPerformanceStatus_NoRecentData_WhenNoRecentRequests()
        {
            // Arrange
            statisticsServiceMock
                .Setup(s => s.GetProviderNames())
                .Returns(["Weather"]);

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 200,
                    OverallRequestCount = 100,
                    RecentAverageMs = null, // No recent data
                    RecentRequestCount = 0
                });

            // Act
            var result = controller.GetPerformanceStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PerformanceAnomalyResponse>(okResult.Value);
            var provider = response.Providers[0];
            Assert.False(provider.IsAnomaly);
            Assert.Equal("No Recent Data", provider.Status);
        }

        [Fact]
        public void GetPerformanceStatus_WithNoProviders_ReturnsEmptyList()
        {
            // Arrange
            statisticsServiceMock
                .Setup(s => s.GetProviderNames())
                .Returns([]);

            // Act
            var result = controller.GetPerformanceStatus();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PerformanceAnomalyResponse>(okResult.Value);
            Assert.Empty(response.Providers);
        }

        #endregion

        #region ResetStatistics Tests

        [Fact]
        public void ResetStatistics_ReturnsNoContent()
        {
            // Act
            var result = controller.ResetStatistics();

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public void ResetStatistics_CallsServiceReset()
        {
            // Act
            controller.ResetStatistics();

            // Assert
            statisticsServiceMock.Verify(s => s.Reset(), Times.Once);
        }

        [Fact]
        public void ResetStatistics_AfterReset_GetStatisticsReturnsEmpty()
        {
            // Arrange
            var emptyResponse = new StatisticsResponse { Providers = [] };

            statisticsServiceMock
                .Setup(s => s.GetStatistics())
                .Returns(emptyResponse);

            // Act
            controller.ResetStatistics();
            var result = controller.GetStatistics();

            // Assert
            statisticsServiceMock.Verify(s => s.Reset(), Times.Once);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<StatisticsResponse>(okResult.Value);
            Assert.Empty(response.Providers);
        }

        #endregion
    }
}