using AggregatorService.Services.Statistics;

namespace AggregatorService.Services.Monitoring
{
    /// <summary>
    /// Background service that periodically analyzes performance statistics
    /// and logs anomalies when provider response times exceed thresholds
    /// </summary>
    public class PerformanceMonitorService(
        IStatisticsService statisticsService,
        ILogger<PerformanceMonitorService> logger,
        IConfiguration configuration) : BackgroundService
    {

        private const string ConfigKey = "PerformanceMonitor";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = configuration.GetValue($"{ConfigKey}:Enabled", true);
            if (!enabled)
            {
                logger.LogInformation("Performance monitoring is disabled");
                return;
            }

            var checkIntervalSeconds = configuration.GetValue($"{ConfigKey}:CheckIntervalSeconds", 30);
            var recentWindowMinutes = configuration.GetValue($"{ConfigKey}:RecentWindowMinutes", 5);
            var anomalyThresholdPercent = configuration.GetValue($"{ConfigKey}:AnomalyThresholdPercent", 50);

            logger.LogInformation(
                "Performance monitor started. Interval: {Interval}s, Window: {Window}m, Threshold: {Threshold}%",
                checkIntervalSeconds, recentWindowMinutes, anomalyThresholdPercent);

            var checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
            var recentWindow = TimeSpan.FromMinutes(recentWindowMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(checkInterval, stoppingToken);
                    AnalyzePerformance(recentWindow, anomalyThresholdPercent);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during performance analysis");
                }
            }

            logger.LogInformation("Performance monitor stopped");
        }

        private void AnalyzePerformance(TimeSpan recentWindow, int anomalyThresholdPercent)
        {
            var providerNames = statisticsService.GetProviderNames();

            foreach (var providerName in providerNames)
            {
                var snapshot = statisticsService.GetProviderSnapshot(providerName, recentWindow);

                // Skip if not enough data
                if (snapshot.OverallRequestCount < 5 || snapshot.RecentRequestCount < 2)
                {
                    continue;
                }

                if (!snapshot.RecentAverageMs.HasValue)
                {
                    continue;
                }

                var recentAvg = snapshot.RecentAverageMs.Value;
                var overallAvg = snapshot.OverallAverageMs;

                // Calculate percentage increase
                var percentageIncrease = ((recentAvg - overallAvg) / overallAvg) * 100;

                if (percentageIncrease > anomalyThresholdPercent)
                {
                    logger.LogWarning(
                        "PERFORMANCE ANOMALY DETECTED - Provider: {Provider} | " +
                        "Recent avg: {RecentAvg:F2}ms ({RecentCount} requests) | " +
                        "Overall avg: {OverallAvg:F2}ms ({OverallCount} requests) | " +
                        "Degradation: {Percentage:F1}% (threshold: {Threshold}%)",
                        providerName,
                        recentAvg,
                        snapshot.RecentRequestCount,
                        overallAvg,
                        snapshot.OverallRequestCount,
                        percentageIncrease,
                        anomalyThresholdPercent);
                }
                else
                {
                    logger.LogDebug(
                        "Provider {Provider} performance normal. Recent: {RecentAvg:F2}ms, Overall: {OverallAvg:F2}ms",
                        providerName, recentAvg, overallAvg);
                }
            }
        }
    }
}