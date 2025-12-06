using AggregatorService.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace AggregatorService.Tests.Middleware
{
    public class GlobalErrorHandlerMiddlewareTests
    {
        private readonly Mock<ILogger<GlobalErrorHandlerMiddleware>> _loggerMock;

        public GlobalErrorHandlerMiddlewareTests()
        {
            _loggerMock = new Mock<ILogger<GlobalErrorHandlerMiddleware>>();
        }

        #region Success Path Tests

        [Fact]
        public async Task InvokeAsync_WhenNoException_CallsNextDelegate()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new GlobalErrorHandlerMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
        }

        #endregion

        #region Exception Handling Tests

        [Fact]
        public async Task InvokeAsync_WhenUnauthorizedAccessException_Returns401()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            RequestDelegate next = (ctx) => throw new UnauthorizedAccessException("Access denied");
            var middleware = new GlobalErrorHandlerMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(401, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Contains("Access denied", responseBody);
        }

        [Fact]
        public async Task InvokeAsync_WhenArgumentException_Returns400()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            RequestDelegate next = (ctx) => throw new ArgumentException("Invalid parameter");
            var middleware = new GlobalErrorHandlerMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(400, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Contains("Invalid parameter", responseBody);
        }

        [Fact]
        public async Task InvokeAsync_WhenGenericException_Returns500()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            RequestDelegate next = (ctx) => throw new Exception("Something went wrong");
            var middleware = new GlobalErrorHandlerMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(500, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Contains("unexpected error", responseBody.ToLower());
        }

        #endregion

        #region Response Format Tests

        [Fact]
        public async Task InvokeAsync_ReturnsJsonResponse()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            RequestDelegate next = (ctx) => throw new ArgumentException("Bad request");
            var middleware = new GlobalErrorHandlerMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

            var jsonDoc = JsonDocument.Parse(responseBody);
            Assert.True(jsonDoc.RootElement.TryGetProperty("statusCode", out var statusCode));
            Assert.True(jsonDoc.RootElement.TryGetProperty("message", out var message));
            Assert.Equal(400, statusCode.GetInt32());
            Assert.Equal("Bad request", message.GetString());
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task InvokeAsync_LogsError()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var exception = new Exception("Test exception");
            RequestDelegate next = (ctx) => throw exception;
            var middleware = new GlobalErrorHandlerMiddleware(next, _loggerMock.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}