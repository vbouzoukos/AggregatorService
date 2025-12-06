using AggregatorService.Services.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using System.Text;
using System.Text.Json;

namespace AggregatorService.Tests.Services
{
    public class CacheServiceTests
    {
        private readonly Mock<IDistributedCache> _distributedCacheMock;
        private readonly CacheService _cacheService;

        public CacheServiceTests()
        {
            _distributedCacheMock = new Mock<IDistributedCache>();
            _cacheService = new CacheService(_distributedCacheMock.Object);
        }

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_WhenKeyExists_ReturnsDeserializedValue()
        {
            // Arrange
            var testData = new TestData { Id = 1, Name = "Test" };
            var serialized = JsonSerializer.Serialize(testData);
            var bytes = Encoding.UTF8.GetBytes(serialized);

            _distributedCacheMock
                .Setup(c => c.GetAsync("test-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync(bytes);

            // Act
            var result = await _cacheService.GetAsync<TestData>("test-key");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test", result.Name);
        }

        [Fact]
        public async Task GetAsync_WhenKeyDoesNotExist_ReturnsDefault()
        {
            // Arrange
            _distributedCacheMock
                .Setup(c => c.GetAsync("missing-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _cacheService.GetAsync<TestData>("missing-key");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_WhenEmptyBytes_ReturnsDefault()
        {
            // Arrange
            _distributedCacheMock
                .Setup(c => c.GetAsync("empty-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<byte>());

            // Act
            var result = await _cacheService.GetAsync<TestData>("empty-key");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region SetAsync Tests

        [Fact]
        public async Task SetAsync_SerializesAndStoresValue()
        {
            // Arrange
            var testData = new TestData { Id = 1, Name = "Test" };
            var expiration = TimeSpan.FromMinutes(10);
            byte[]? capturedValue = null;

            _distributedCacheMock
                .Setup(c => c.SetAsync(
                    "test-key",
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                    (key, value, options, token) => capturedValue = value)
                .Returns(Task.CompletedTask);

            // Act
            await _cacheService.SetAsync("test-key", testData, expiration);

            // Assert
            Assert.NotNull(capturedValue);
            var serializedString = Encoding.UTF8.GetString(capturedValue);
            var deserialized = JsonSerializer.Deserialize<TestData>(serializedString);
            Assert.NotNull(deserialized);
            Assert.Equal(1, deserialized.Id);
            Assert.Equal("Test", deserialized.Name);
        }

        [Fact]
        public async Task SetAsync_SetsCorrectExpiration()
        {
            // Arrange
            var testData = new TestData { Id = 1, Name = "Test" };
            var expiration = TimeSpan.FromMinutes(30);
            DistributedCacheEntryOptions? capturedOptions = null;

            _distributedCacheMock
                .Setup(c => c.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                    (key, value, options, token) => capturedOptions = options)
                .Returns(Task.CompletedTask);

            // Act
            await _cacheService.SetAsync("test-key", testData, expiration);

            // Assert
            Assert.NotNull(capturedOptions);
            Assert.Equal(expiration, capturedOptions.AbsoluteExpirationRelativeToNow);
        }

        #endregion

        #region RemoveAsync Tests

        [Fact]
        public async Task RemoveAsync_CallsDistributedCacheRemove()
        {
            // Arrange
            _distributedCacheMock
                .Setup(c => c.RemoveAsync("test-key", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _cacheService.RemoveAsync("test-key");

            // Assert
            _distributedCacheMock.Verify(
                c => c.RemoveAsync("test-key", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Test Helper Classes

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        #endregion
    }
}