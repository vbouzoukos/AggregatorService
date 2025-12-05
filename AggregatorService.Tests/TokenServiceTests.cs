using AggregatorService.Models.Requests;
using AggregatorService.Services.Authorise;
using Microsoft.Extensions.Configuration;
using Moq;

namespace AggregatorService.Tests.Services
{
    public class TokenServiceTests
    {
        private readonly Mock<IIdentityProvider> identityProviderMock;
        private readonly IConfiguration configuration;
        private readonly TokenService tokenService;

        public TokenServiceTests()
        {
            identityProviderMock = new Mock<IIdentityProvider>();

            // Setup test configuration
            var configValues = new Dictionary<string, string?>
            {
                { "JwtSettings:SecretKey", "AggregatorService-Dev-SecretKey-2024-NotForProduction-Use-Only!!" },
                { "JwtSettings:Issuer", "AggregatorService" },
                { "JwtSettings:Audience", "AggregatorService-Clients" },
                { "JwtSettings:ExpirationInMinutes", "60" }
            };

            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            tokenService = new TokenService(configuration, identityProviderMock.Object);
        }

        [Fact]
        public void GenerateToken_ValidCredentials_ReturnsToken()
        {
            // Arrange
            var request = new AuthenticationRequest
            {
                Username = "admin",
                Password = "admin123"
            };

            identityProviderMock
                .Setup(x => x.Authenticate(request.Username, request.Password));

            // Act
            var result = tokenService.GenerateToken(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Token);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public void GenerateToken_ValidCredentials_ReturnsTokenWithCorrectExpiration()
        {
            // Arrange
            var request = new AuthenticationRequest
            {
                Username = "admin",
                Password = "admin123"
            };

            identityProviderMock
                .Setup(x => x.Authenticate(request.Username, request.Password));

            var utcNow = DateTime.UtcNow;

            // Act
            var result = tokenService.GenerateToken(request);

            // Assert
            Assert.True(result.ExpiresAt >= utcNow.AddMinutes(59));
            Assert.True(result.ExpiresAt <= utcNow.AddMinutes(61));
        }

        [Fact]
        public void GenerateToken_InvalidCredentials_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new AuthenticationRequest
            {
                Username = "invalid",
                Password = "invalid"
            };

            identityProviderMock
                .Setup(x => x.Authenticate(request.Username, request.Password))
                .Throws(new UnauthorizedAccessException("Invalid username or password"));

            // Act & Assert
            var exception = Assert.Throws<UnauthorizedAccessException>(() => tokenService.GenerateToken(request));
            Assert.Equal("Invalid username or password", exception.Message);
        }

        [Fact]
        public void GenerateToken_CallsIdentityProviderOnce()
        {
            // Arrange
            var request = new AuthenticationRequest
            {
                Username = "admin",
                Password = "admin123"
            };

            identityProviderMock
                .Setup(x => x.Authenticate(request.Username, request.Password));

            // Act
            tokenService.GenerateToken(request);

            // Assert
            identityProviderMock.Verify(x => x.Authenticate(request.Username, request.Password), Times.Once);
        }
    }
}