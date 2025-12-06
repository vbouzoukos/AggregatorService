using AggregatorService.Services.Statistics;

namespace AggregatorService.Tests.Services
{
    public class StatisticsServiceTests
    {
        private readonly StatisticsService _service;

        public StatisticsServiceTests()
        {
            _service = new StatisticsService();
        }

        #region RecordRequest Tests

        [Fact]
        public void RecordRequest_AddsRequestToStatistics()
        {
            // Act
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "TestProvider");
            Assert.NotNull(providerStats);
            Assert.Equal(1, providerStats.TotalRequests);
            Assert.Equal(1, providerStats.SuccessfulRequests);
        }

        [Fact]
        public void RecordRequest_TracksFailedRequests()
        {
            // Act
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), false);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(1, providerStats.TotalRequests);
            Assert.Equal(0, providerStats.SuccessfulRequests);
            Assert.Equal(1, providerStats.FailedRequests);
        }

        [Fact]
        public void RecordRequest_TracksMixedSuccessAndFailure()
        {
            // Act
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), false);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(3, providerStats.TotalRequests);
            Assert.Equal(2, providerStats.SuccessfulRequests);
            Assert.Equal(1, providerStats.FailedRequests);
        }

        [Fact]
        public void RecordRequest_CalculatesAverageResponseTime()
        {
            // Act
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(200), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(150, providerStats.AverageResponseTimeMs);
        }

        [Fact]
        public void RecordRequest_CalculatesAverageWithMultipleValues()
        {
            // Act
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(200), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(300), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(200, providerStats.AverageResponseTimeMs);
        }

        [Fact]
        public void RecordRequest_TracksMultipleProviders()
        {
            // Act
            _service.RecordRequest("Provider1", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("Provider2", TimeSpan.FromMilliseconds(200), true);

            // Assert
            var stats = _service.GetStatistics();
            Assert.Equal(2, stats.Providers.Count);
            Assert.Contains(stats.Providers, p => p.ProviderName == "Provider1");
            Assert.Contains(stats.Providers, p => p.ProviderName == "Provider2");
        }

        #endregion

        #region Performance Buckets Tests

        [Fact]
        public void RecordRequest_CategorizesFastRequests()
        {
            // Act - Fast is < 100ms
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(50), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(1, providerStats.PerformanceBuckets.Fast);
            Assert.Equal(0, providerStats.PerformanceBuckets.Average);
            Assert.Equal(0, providerStats.PerformanceBuckets.Slow);
        }

        [Fact]
        public void RecordRequest_CategorizesAverageRequests()
        {
            // Act - Average is >= 100ms and < 200ms
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(150), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(0, providerStats.PerformanceBuckets.Fast);
            Assert.Equal(1, providerStats.PerformanceBuckets.Average);
            Assert.Equal(0, providerStats.PerformanceBuckets.Slow);
        }

        [Fact]
        public void RecordRequest_CategorizesSlowRequests()
        {
            // Act - Slow is >= 200ms
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(250), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(0, providerStats.PerformanceBuckets.Fast);
            Assert.Equal(0, providerStats.PerformanceBuckets.Average);
            Assert.Equal(1, providerStats.PerformanceBuckets.Slow);
        }

        [Fact]
        public void RecordRequest_HandlesBoundaryAt100ms_IsAverage()
        {
            // Act - Exactly 100ms should be "Average" (>= 100ms && < 200ms)
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(0, providerStats.PerformanceBuckets.Fast);
            Assert.Equal(1, providerStats.PerformanceBuckets.Average);
            Assert.Equal(0, providerStats.PerformanceBuckets.Slow);
        }

        [Fact]
        public void RecordRequest_HandlesBoundaryAt200ms_IsSlow()
        {
            // Act - Exactly 200ms should be "Slow" (>= 200ms)
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(200), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(0, providerStats.PerformanceBuckets.Fast);
            Assert.Equal(0, providerStats.PerformanceBuckets.Average);
            Assert.Equal(1, providerStats.PerformanceBuckets.Slow);
        }

        [Fact]
        public void RecordRequest_DistributesAcrossBuckets()
        {
            // Act
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(50), true);   // Fast
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(80), true);   // Fast
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(150), true);  // Average
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(300), true);  // Slow
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(500), true);  // Slow

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(2, providerStats.PerformanceBuckets.Fast);
            Assert.Equal(1, providerStats.PerformanceBuckets.Average);
            Assert.Equal(2, providerStats.PerformanceBuckets.Slow);
        }

        #endregion

        #region GetStatistics Tests

        [Fact]
        public void GetStatistics_ReturnsTimestamp()
        {
            // Arrange
            var beforeCall = DateTime.UtcNow;
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);

            // Act
            var stats = _service.GetStatistics();

            // Assert
            Assert.True(stats.Timestamp >= beforeCall);
            Assert.True(stats.Timestamp <= DateTime.UtcNow);
        }

        [Fact]
        public void GetStatistics_WithNoRecords_ReturnsEmptyProviders()
        {
            // Act
            var stats = _service.GetStatistics();

            // Assert
            Assert.Empty(stats.Providers);
        }

        [Fact]
        public void GetStatistics_RoundsAverageToTwoDecimals()
        {
            // Act
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(101), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(102), true);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(101, providerStats.AverageResponseTimeMs);
        }

        #endregion

        #region GetProviderSnapshot Tests

        [Fact]
        public void GetProviderSnapshot_ReturnsCorrectOverallStats()
        {
            // Arrange
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(200), true);

            // Act
            var snapshot = _service.GetProviderSnapshot("TestProvider", TimeSpan.FromMinutes(5));

            // Assert
            Assert.Equal("TestProvider", snapshot.ProviderName);
            Assert.Equal(150, snapshot.OverallAverageMs);
            Assert.Equal(2, snapshot.OverallRequestCount);
        }

        [Fact]
        public void GetProviderSnapshot_IncludesRecentRequests()
        {
            // Arrange - Request was just made, should be in recent window
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);

            // Act
            var snapshot = _service.GetProviderSnapshot("TestProvider", TimeSpan.FromMinutes(5));

            // Assert
            Assert.Equal(1, snapshot.RecentRequestCount);
            Assert.NotNull(snapshot.RecentAverageMs);
            Assert.Equal(100, snapshot.RecentAverageMs);
        }

        [Fact]
        public void GetProviderSnapshot_ForNonExistentProvider_ReturnsEmptySnapshot()
        {
            // Act
            var snapshot = _service.GetProviderSnapshot("NonExistent", TimeSpan.FromMinutes(5));

            // Assert
            Assert.Equal("NonExistent", snapshot.ProviderName);
            Assert.Equal(0, snapshot.OverallAverageMs);
            Assert.Equal(0, snapshot.OverallRequestCount);
            Assert.Null(snapshot.RecentAverageMs);
            Assert.Equal(0, snapshot.RecentRequestCount);
        }

        [Fact]
        public void GetProviderSnapshot_RoundsAverageToTwoDecimals()
        {
            // Arrange
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(101), true);
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(102), true);

            // Act
            var snapshot = _service.GetProviderSnapshot("TestProvider", TimeSpan.FromMinutes(5));

            // Assert
            Assert.Equal(101, snapshot.OverallAverageMs);
            Assert.Equal(101, snapshot.RecentAverageMs);
        }

        #endregion

        #region GetProviderNames Tests

        [Fact]
        public void GetProviderNames_ReturnsAllProviders()
        {
            // Arrange
            _service.RecordRequest("Provider1", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("Provider2", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("Provider3", TimeSpan.FromMilliseconds(100), true);

            // Act
            var names = _service.GetProviderNames().ToList();

            // Assert
            Assert.Equal(3, names.Count);
            Assert.Contains("Provider1", names);
            Assert.Contains("Provider2", names);
            Assert.Contains("Provider3", names);
        }

        [Fact]
        public void GetProviderNames_WithNoProviders_ReturnsEmpty()
        {
            // Act
            var names = _service.GetProviderNames().ToList();

            // Assert
            Assert.Empty(names);
        }

        [Fact]
        public void GetProviderNames_DoesNotReturnDuplicates()
        {
            // Arrange
            _service.RecordRequest("Provider1", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("Provider1", TimeSpan.FromMilliseconds(200), true);
            _service.RecordRequest("Provider1", TimeSpan.FromMilliseconds(300), true);

            // Act
            var names = _service.GetProviderNames().ToList();

            // Assert
            Assert.Single(names);
            Assert.Equal("Provider1", names[0]);
        }

        #endregion

        #region Reset Tests

        [Fact]
        public void Reset_ClearsAllStatistics()
        {
            // Arrange
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);

            // Act
            _service.Reset();
            var stats = _service.GetStatistics();

            // Assert
            Assert.Empty(stats.Providers);
        }

        [Fact]
        public void Reset_ClearsAllProviders()
        {
            // Arrange
            _service.RecordRequest("Provider1", TimeSpan.FromMilliseconds(100), true);
            _service.RecordRequest("Provider2", TimeSpan.FromMilliseconds(100), true);

            // Act
            _service.Reset();
            var names = _service.GetProviderNames().ToList();

            // Assert
            Assert.Empty(names);
        }

        [Fact]
        public void Reset_AllowsNewRecordsAfterReset()
        {
            // Arrange
            _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true);
            _service.Reset();

            // Act
            _service.RecordRequest("NewProvider", TimeSpan.FromMilliseconds(200), true);
            var stats = _service.GetStatistics();

            // Assert
            Assert.Single(stats.Providers);
            Assert.Equal("NewProvider", stats.Providers[0].ProviderName);
            Assert.Equal(200, stats.Providers[0].AverageResponseTimeMs);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task RecordRequest_IsThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();
            var requestCount = 1000;

            // Act - Simulate concurrent requests
            for (int i = 0; i < requestCount; i++)
            {
                tasks.Add(Task.Run(() =>
                    _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true)));
            }
            await Task.WhenAll(tasks);

            // Assert
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.First(p => p.ProviderName == "TestProvider");
            Assert.Equal(requestCount, providerStats.TotalRequests);
        }

        [Fact]
        public async Task RecordRequest_IsThreadSafe_WithMultipleProviders()
        {
            // Arrange
            var tasks = new List<Task>();
            var requestsPerProvider = 500;
            var providers = new[] { "Provider1", "Provider2", "Provider3" };

            // Act - Simulate concurrent requests to multiple providers
            foreach (var provider in providers)
            {
                for (int i = 0; i < requestsPerProvider; i++)
                {
                    var p = provider; // Capture for closure
                    tasks.Add(Task.Run(() =>
                        _service.RecordRequest(p, TimeSpan.FromMilliseconds(100), true)));
                }
            }
            await Task.WhenAll(tasks);

            // Assert
            var stats = _service.GetStatistics();
            Assert.Equal(3, stats.Providers.Count);
            foreach (var provider in providers)
            {
                var providerStats = stats.Providers.First(p => p.ProviderName == provider);
                Assert.Equal(requestsPerProvider, providerStats.TotalRequests);
            }
        }

        [Fact]
        public async Task GetStatistics_IsThreadSafe_WhileRecording()
        {
            // Arrange
            var recordTasks = new List<Task>();
            var readTasks = new List<Task<int>>();
            var requestCount = 100;

            // Act - Simulate concurrent reads and writes
            for (int i = 0; i < requestCount; i++)
            {
                recordTasks.Add(Task.Run(() =>
                    _service.RecordRequest("TestProvider", TimeSpan.FromMilliseconds(100), true)));

                readTasks.Add(Task.Run(() =>
                    _service.GetStatistics().Providers.Count));
            }

            await Task.WhenAll(recordTasks);
            await Task.WhenAll(readTasks);

            // Assert - No exceptions should be thrown, and final count should be correct
            var stats = _service.GetStatistics();
            var providerStats = stats.Providers.FirstOrDefault(p => p.ProviderName == "TestProvider");
            Assert.NotNull(providerStats);
            Assert.Equal(requestCount, providerStats.TotalRequests);
        }

        #endregion
    }
}