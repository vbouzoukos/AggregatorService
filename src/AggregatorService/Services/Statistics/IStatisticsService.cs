using AggregatorService.Models.Responses;

namespace AggregatorService.Services.Statistics
{
    /// <summary>
    /// Service for recording and retrieving API request statistics
    /// </summary>
    public interface IStatisticsService
    {
        /// <summary>
        /// Records a request statistic for a provider
        /// </summary>
        void RecordRequest(string providerName, TimeSpan responseTime, bool isSuccess);

        /// <summary>
        /// Gets statistics for all providers
        /// </summary>
        StatisticsResponse GetStatistics();

        /// <summary>
        /// Gets a performance snapshot for a specific provider
        /// Used for anomaly detection
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <param name="recentWindow">Time window for recent statistics</param>
        ProviderPerformanceSnapshot GetProviderSnapshot(string providerName, TimeSpan recentWindow);

        /// <summary>
        /// Gets all registered provider names
        /// </summary>
        IEnumerable<string> GetProviderNames();

        /// <summary>
        /// Resets all statistics
        /// </summary>
        void Reset();
    }
    /// <summary>
    /// Snapshot of provider performance for anomaly detection
    /// </summary>
    public class ProviderPerformanceSnapshot
    {
        public string ProviderName { get; set; } = string.Empty;
        public double OverallAverageMs { get; set; }
        public int OverallRequestCount { get; set; }
        public double? RecentAverageMs { get; set; }
        public int RecentRequestCount { get; set; }
    }
}