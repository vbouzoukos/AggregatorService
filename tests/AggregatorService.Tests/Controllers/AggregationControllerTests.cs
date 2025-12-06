using AggregatorService.Controllers;
using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Aggregation;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AggregatorService.Tests.Controllers
{
    public class AggregationControllerTests
    {
        private readonly Mock<IAggregationService> aggregationServiceMock;
        private readonly AggregationController controller;

        public AggregationControllerTests()
        {
            aggregationServiceMock = new Mock<IAggregationService>();
            controller = new AggregationController(aggregationServiceMock.Object);
        }

        [Fact]
        public async Task Aggregate_WithValidRequest_ReturnsOkWithResponse()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "city", "London" },
                    { "units", "metric" }
                }
            };

            var expectedResponse = new AggregationResponse
            {
                Timestamp = DateTime.UtcNow,
                TotalResponseTime = TimeSpan.FromMilliseconds(250),
                ProvidersQueried = 1,
                SuccessfulResponses = 1,
                Results =
                [
                    new ApiResponse
                    {
                        Provider = "weather",
                        IsSuccess = true,
                        Data = new { temp = 15.5 },
                        ResponseTime = TimeSpan.FromMilliseconds(200)
                    }
                ]
            };

            aggregationServiceMock
                .Setup(s => s.AggregateAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await controller.Aggregate(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AggregationResponse>(okResult.Value);
            Assert.Equal(1, response.ProvidersQueried);
            Assert.Equal(1, response.SuccessfulResponses);
            Assert.Single(response.Results);
        }

        [Fact]
        public async Task Aggregate_WithMultipleProviders_ReturnsAllResults()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "city", "London" },
                    { "query", "technology" }
                }
            };

            var expectedResponse = new AggregationResponse
            {
                ProvidersQueried = 2,
                SuccessfulResponses = 2,
                Results =
                [
                    new ApiResponse { Provider = "weather", IsSuccess = true },
                    new ApiResponse { Provider = "news", IsSuccess = true }
                ]
            };

            aggregationServiceMock
                .Setup(s => s.AggregateAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await controller.Aggregate(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AggregationResponse>(okResult.Value);
            Assert.Equal(2, response.ProvidersQueried);
            Assert.Equal(2, response.Results.Count);
        }

        [Fact]
        public async Task Aggregate_WithNoMatchingProviders_ReturnsEmptyResults()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "unknownParam", "value" }
                }
            };

            var expectedResponse = new AggregationResponse
            {
                ProvidersQueried = 0,
                SuccessfulResponses = 0,
                Results = []
            };

            aggregationServiceMock
                .Setup(s => s.AggregateAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await controller.Aggregate(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AggregationResponse>(okResult.Value);
            Assert.Equal(0, response.ProvidersQueried);
            Assert.Empty(response.Results);
        }

        [Fact]
        public async Task Aggregate_WithPartialFailure_ReturnsPartialResults()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "city", "London" }
                }
            };

            var expectedResponse = new AggregationResponse
            {
                ProvidersQueried = 2,
                SuccessfulResponses = 1,
                Results =
                [
                    new ApiResponse { Provider = "weather", IsSuccess = true },
                    new ApiResponse { Provider = "news", IsSuccess = false, ErrorMessage = "API unavailable" }
                ]
            };

            aggregationServiceMock
                .Setup(s => s.AggregateAsync(request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await controller.Aggregate(request, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AggregationResponse>(okResult.Value);
            Assert.Equal(2, response.ProvidersQueried);
            Assert.Equal(1, response.SuccessfulResponses);
        }

        [Fact]
        public async Task Aggregate_CallsServiceWithCorrectParameters()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    { "city", "Paris" },
                    { "country", "FR" }
                }
            };

            aggregationServiceMock
                .Setup(s => s.AggregateAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AggregationResponse());

            // Act
            await controller.Aggregate(request, CancellationToken.None);

            // Assert
            aggregationServiceMock.Verify(
                s => s.AggregateAsync(
                    It.Is<AggregationRequest>(r =>
                        r.Parameters["city"] == "Paris" &&
                        r.Parameters["country"] == "FR"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Aggregate_PassesCancellationToken()
        {
            // Arrange
            var request = new AggregationRequest
            {
                Parameters = new Dictionary<string, string> { { "city", "London" } }
            };

            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            aggregationServiceMock
                .Setup(s => s.AggregateAsync(It.IsAny<AggregationRequest>(), token))
                .ReturnsAsync(new AggregationResponse());

            // Act
            await controller.Aggregate(request, token);

            // Assert
            aggregationServiceMock.Verify(
                s => s.AggregateAsync(It.IsAny<AggregationRequest>(), token),
                Times.Once);
        }
    }
}