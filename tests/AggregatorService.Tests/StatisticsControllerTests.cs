using AggregatorService.Controllers;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Statistics;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AggregatorService.Tests.Controllers
{
    public class StatisticsControllerTests
    {
        private readonly Mock<IStatisticsService> statisticsServiceMock;
        private readonly StatisticsController controller;

        public StatisticsControllerTests()
        {
            statisticsServiceMock = new Mock<IStatisticsService>();
            controller = new StatisticsController(statisticsServiceMock.Object);
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
                        ProviderName = "weather",
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
            Assert.Equal("weather", response.Providers[0].ProviderName);
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
                    new ProviderStatistics { ProviderName = "weather", TotalRequests = 50 },
                    new ProviderStatistics { ProviderName = "news", TotalRequests = 30 },
                    new ProviderStatistics { ProviderName = "twitter", TotalRequests = 20 }
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
                        ProviderName = "weather",
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