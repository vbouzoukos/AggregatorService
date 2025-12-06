using AggregatorService.Services.Authorise;

namespace AggregatorService.Tests.Services
{
    public class IdentityProviderTests
    {
        private readonly IdentityProvider _identityProvider;

        public IdentityProviderTests()
        {
            _identityProvider = new IdentityProvider();
        }

        #region Valid Credentials Tests

        [Theory]
        [InlineData("admin", "admin123")]
        [InlineData("user", "user123")]
        [InlineData("demo", "demo123")]
        public void Authenticate_WithValidCredentials_DoesNotThrow(string username, string password)
        {
            // Act & Assert - Should not throw
            var exception = Record.Exception(() => _identityProvider.Authenticate(username, password));
            Assert.Null(exception);
        }

        #endregion

        #region Invalid Credentials Tests

        [Fact]
        public void Authenticate_WithInvalidUsername_ThrowsUnauthorizedAccessException()
        {
            // Act & Assert
            var exception = Assert.Throws<UnauthorizedAccessException>(
                () => _identityProvider.Authenticate("invaliduser", "admin123"));
            Assert.Equal("Invalid username or password", exception.Message);
        }

        [Fact]
        public void Authenticate_WithInvalidPassword_ThrowsUnauthorizedAccessException()
        {
            // Act & Assert
            var exception = Assert.Throws<UnauthorizedAccessException>(
                () => _identityProvider.Authenticate("admin", "wrongpassword"));
            Assert.Equal("Invalid username or password", exception.Message);
        }

        [Fact]
        public void Authenticate_WithEmptyUsername_ThrowsUnauthorizedAccessException()
        {
            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(
                () => _identityProvider.Authenticate("", "admin123"));
        }

        [Fact]
        public void Authenticate_WithEmptyPassword_ThrowsUnauthorizedAccessException()
        {
            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(
                () => _identityProvider.Authenticate("admin", ""));
        }

        [Fact]
        public void Authenticate_IsCaseSensitive()
        {
            // Act & Assert - Username should be case-sensitive
            Assert.Throws<UnauthorizedAccessException>(
                () => _identityProvider.Authenticate("Admin", "admin123"));
        }

        #endregion
    }
}