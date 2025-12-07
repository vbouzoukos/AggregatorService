using AggregatorService.Services.Monitoring;
using AggregatorService.Services.Statistics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AggregatorService.Tests.Services.Monitor
{
    public class PerformanceMonitorServiceTests
    {
        private readonly Mock<IStatisticsService> statisticsServiceMock;
        private readonly Mock<ILogger<PerformanceMonitorService>> loggerMock;

        public PerformanceMonitorServiceTests()
        {
            statisticsServiceMock = new Mock<IStatisticsService>();
            loggerMock = new Mock<ILogger<PerformanceMonitorService>>();
        }

        private static IConfiguration CreateConfiguration(
            bool enabled = true,
            int checkIntervalSeconds = 1,
            int recentWindowMinutes = 5,
            int anomalyThresholdPercent = 50)
        {
            var configValues = new Dictionary<string, string?>
            {
                { "PerformanceMonitor:Enabled", enabled.ToString() },
                { "PerformanceMonitor:CheckIntervalSeconds", checkIntervalSeconds.ToString() },
                { "PerformanceMonitor:RecentWindowMinutes", recentWindowMinutes.ToString() },
                { "PerformanceMonitor:AnomalyThresholdPercent", anomalyThresholdPercent.ToString() }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
        }

        #region Service Lifecycle Tests

        [Fact]
        public async Task ExecuteAsync_WhenDisabled_ExitsImmediately()
        {
            // Arrange
            var configuration = CreateConfiguration(enabled: false);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(100); // Give it time to log
            await service.StopAsync(cts.Token);

            // Assert
            statisticsServiceMock.Verify(
                s => s.GetProviderNames(),
                Times.Never);

            VerifyLog(LogLevel.Information, "disabled");
        }

        [Fact]
        public async Task ExecuteAsync_WhenEnabled_LogsStartupMessage()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 60);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns([]);

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            VerifyLog(LogLevel.Information, "started");
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelled_LogsStoppedMessage()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns([]);

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(50);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            VerifyLog(LogLevel.Information, "stopped");
        }

        [Fact]
        public async Task ExecuteAsync_PerformsAnalysisAfterInterval()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 100,
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500); // Wait for at least one interval
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            statisticsServiceMock.Verify(
                s => s.GetProviderNames(),
                Times.AtLeastOnce);
        }

        #endregion

        #region Anomaly Detection Tests

        [Fact]
        public async Task ExecuteAsync_WhenPerformanceDegraded_LogsWarning()
        {
            // Arrange
            var configuration = CreateConfiguration(
                checkIntervalSeconds: 1,
                anomalyThresholdPercent: 50);

            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 200, // 100% increase - exceeds 50% threshold
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            VerifyLog(LogLevel.Warning, "ANOMALY");
        }

        [Fact]
        public async Task ExecuteAsync_WhenPerformanceNormal_LogsDebug()
        {
            // Arrange
            var configuration = CreateConfiguration(
                checkIntervalSeconds: 1,
                anomalyThresholdPercent: 50);

            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 110, // 10% increase - below 50% threshold
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            VerifyLog(LogLevel.Debug, "normal");
        }

        [Fact]
        public async Task ExecuteAsync_WhenExactlyAtThreshold_DoesNotLogWarning()
        {
            // Arrange
            var configuration = CreateConfiguration(
                checkIntervalSeconds: 1,
                anomalyThresholdPercent: 50);

            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 150, // Exactly 50% - not exceeding threshold
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert - Should log debug (normal), not warning
            VerifyLog(LogLevel.Debug, "normal");
            VerifyLogNever(LogLevel.Warning, "ANOMALY");
        }

        #endregion

        #region Insufficient Data Tests

        [Fact]
        public async Task ExecuteAsync_WhenInsufficientOverallRequests_SkipsAnalysis()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 3, // Less than 5 required
                    RecentAverageMs = 200,
                    RecentRequestCount = 2
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert - Should not log any anomaly or normal status
            VerifyLogNever(LogLevel.Warning, "ANOMALY");
            VerifyLogNever(LogLevel.Debug, "normal");
        }

        [Fact]
        public async Task ExecuteAsync_WhenInsufficientRecentRequests_SkipsAnalysis()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 200,
                    RecentRequestCount = 1 // Less than 2 required
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            VerifyLogNever(LogLevel.Warning, "ANOMALY");
            VerifyLogNever(LogLevel.Debug, "normal");
        }

        [Fact]
        public async Task ExecuteAsync_WhenNoRecentAverage_SkipsAnalysis()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = null, // No recent data
                    RecentRequestCount = 0
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            VerifyLogNever(LogLevel.Warning, "ANOMALY");
            VerifyLogNever(LogLevel.Debug, "normal");
        }

        #endregion

        #region Multiple Providers Tests

        [Fact]
        public async Task ExecuteAsync_WithMultipleProviders_AnalyzesAll()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames())
                .Returns(["Weather", "News", "Books"]);

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 110,
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert
            statisticsServiceMock.Verify(
                s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()),
                Times.AtLeastOnce);
            statisticsServiceMock.Verify(
                s => s.GetProviderSnapshot("News", It.IsAny<TimeSpan>()),
                Times.AtLeastOnce);
            statisticsServiceMock.Verify(
                s => s.GetProviderSnapshot("Books", It.IsAny<TimeSpan>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_WithMixedPerformance_LogsCorrectly()
        {
            // Arrange
            var configuration = CreateConfiguration(
                checkIntervalSeconds: 1,
                anomalyThresholdPercent: 50);

            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames())
                .Returns(["Weather", "News"]);

            // Weather is degraded
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 200, // 100% degradation
                    RecentRequestCount = 5
                });

            // News is normal
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("News", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "News",
                    OverallAverageMs = 150,
                    OverallRequestCount = 10,
                    RecentAverageMs = 160, // ~7% increase
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert - Both should be logged appropriately
            VerifyLog(LogLevel.Warning, "ANOMALY");
            VerifyLog(LogLevel.Debug, "normal");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task ExecuteAsync_WhenExceptionOccurs_LogsErrorAndContinues()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            var callCount = 0;
            statisticsServiceMock.Setup(s => s.GetProviderNames())
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                        throw new InvalidOperationException("Test exception");
                    return ["Weather"];
                });

            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 110,
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(2500); // Wait for multiple intervals
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert - Should log error but continue running
            VerifyLog(LogLevel.Error, "Error");
            Assert.True(callCount >= 2, "Service should continue after exception");
        }

        [Fact]
        public async Task ExecuteAsync_WhenNoProviders_HandlesGracefully()
        {
            // Arrange
            var configuration = CreateConfiguration(checkIntervalSeconds: 1);
            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns([]);

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert - Should not throw, no anomaly logs
            VerifyLogNever(LogLevel.Warning, "ANOMALY");
            statisticsServiceMock.Verify(
                s => s.GetProviderSnapshot(It.IsAny<string>(), It.IsAny<TimeSpan>()),
                Times.Never);
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public async Task ExecuteAsync_UsesConfiguredRecentWindow()
        {
            // Arrange
            var configuration = CreateConfiguration(
                checkIntervalSeconds: 1,
                recentWindowMinutes: 10);

            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 110,
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert - Should use 10 minute window
            statisticsServiceMock.Verify(
                s => s.GetProviderSnapshot("Weather", TimeSpan.FromMinutes(10)),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_UsesConfiguredThreshold()
        {
            // Arrange
            var configuration = CreateConfiguration(
                checkIntervalSeconds: 1,
                anomalyThresholdPercent: 25); // Lower threshold

            var service = new PerformanceMonitorService(
                statisticsServiceMock.Object,
                loggerMock.Object,
                configuration);

            statisticsServiceMock.Setup(s => s.GetProviderNames()).Returns(["Weather"]);
            statisticsServiceMock
                .Setup(s => s.GetProviderSnapshot("Weather", It.IsAny<TimeSpan>()))
                .Returns(new ProviderPerformanceSnapshot
                {
                    ProviderName = "Weather",
                    OverallAverageMs = 100,
                    OverallRequestCount = 10,
                    RecentAverageMs = 130, // 30% increase - exceeds 25% threshold
                    RecentRequestCount = 5
                });

            using var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(1500);
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);

            // Assert - Should detect anomaly at 25% threshold
            VerifyLog(LogLevel.Warning, "ANOMALY");
        }

        #endregion

        #region Helper Methods

        private void VerifyLog(LogLevel level, string messageContains)
        {
            loggerMock.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContains, StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private void VerifyLogNever(LogLevel level, string messageContains)
        {
            loggerMock.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContains, StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        #endregion
    }
}