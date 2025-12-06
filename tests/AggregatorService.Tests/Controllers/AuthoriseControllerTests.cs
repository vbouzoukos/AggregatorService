using AggregatorService.Controllers;
using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Authorise;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AggregatorService.Tests.Controllers
{
    public class AuthoriseControllerTests
    {
        private readonly Mock<ITokenService> tokenServiceMock;
        private readonly AuthoriseController controller;

        public AuthoriseControllerTests()
        {
            tokenServiceMock = new Mock<ITokenService>();
            controller = new AuthoriseController(tokenServiceMock.Object);
        }

        [Fact]
        public void Login_ValidCredentials_ReturnsOkWithToken()
        {
            // Arrange
            var request = new AuthenticationRequest
            {
                Username = "admin",
                Password = "admin123"
            };

            var expectedResponse = new AuthenticationResponse
            {
                Token = "test-jwt-token",
                ExpiresAt = DateTime.UtcNow.AddMinutes(60)
            };

            tokenServiceMock
                .Setup(x => x.GenerateToken(request))
                .Returns(expectedResponse);

            // Act
            var result = controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AuthenticationResponse>(okResult.Value);
            Assert.Equal(expectedResponse.Token, response.Token);
            Assert.Equal(expectedResponse.ExpiresAt, response.ExpiresAt);
        }

        [Fact]
        public void Login_InvalidCredentials_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new AuthenticationRequest
            {
                Username = "invalid",
                Password = "invalid"
            };

            tokenServiceMock
                .Setup(x => x.GenerateToken(request))
                .Throws(new UnauthorizedAccessException("Invalid username or password"));

            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(() => controller.Login(request));
        }

        [Fact]
        public void Login_CallsTokenServiceOnce()
        {
            // Arrange
            var request = new AuthenticationRequest
            {
                Username = "admin",
                Password = "admin123"
            };

            tokenServiceMock
                .Setup(x => x.GenerateToken(request))
                .Returns(new AuthenticationResponse());

            // Act
            controller.Login(request);

            // Assert
            tokenServiceMock.Verify(x => x.GenerateToken(request), Times.Once);
        }
    }
}