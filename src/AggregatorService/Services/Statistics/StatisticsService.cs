using AggregatorService.Models.Responses;
using System.Collections.Concurrent;

namespace AggregatorService.Services.Statistics
{
    /// <summary>
    /// Thread-safe in-memory implementation of statistics service.
    /// Uses lock-based synchronization for atomic operations - chosen over ConcurrentBag
    /// due to AddOrUpdate factory side-effect issues and short critical sections.
    /// </summary>
    public class StatisticsService : IStatisticsService
    {
        private readonly ConcurrentDictionary<string, ProviderRecords> records = new();

        private const int FastThresholdMs = 100;
        private const int AverageThresholdMs = 200;

        /// <summary>
        /// Records a request statistic for a provider
        /// </summary>
        /// <param name="providerName">Provider Name</param>
        /// <param name="responseTime">Response Time</param>
        /// <param name="isSuccess">Is successful</param>
        public void RecordRequest(string providerName, TimeSpan responseTime, bool isSuccess)
        {
            var record = new RequestRecord
            {
                ResponseTimeMs = responseTime.TotalMilliseconds,
                IsSuccess = isSuccess,
                Timestamp = DateTime.UtcNow
            };

            var providerRecords = records.GetOrAdd(providerName, _ => new ProviderRecords());
            providerRecords.Add(record);
        }

        /// <summary>
        /// Gets statistics for all providers
        /// </summary>
        /// <returns>The collection of the statistics records</returns>
        public StatisticsResponse GetStatistics()
        {
            var response = new StatisticsResponse();
            var providerNames = records.Keys.ToList();

            foreach (var providerName in providerNames)
            {
                if (!records.TryGetValue(providerName, out var providerRecords))
                    continue;

                var snapshot = providerRecords.GetSnapshot();

                if (snapshot.Count == 0)
                    continue;

                var providerStats = new ProviderStatistics
                {
                    ProviderName = providerName,
                    TotalRequests = snapshot.Count,
                    SuccessfulRequests = snapshot.Count(r => r.IsSuccess),
                    FailedRequests = snapshot.Count(r => !r.IsSuccess),
                    AverageResponseTimeMs = Math.Round(snapshot.Average(r => r.ResponseTimeMs), 2),
                    PerformanceBuckets = new PerformanceBuckets
                    {
                        Fast = snapshot.Count(r => r.ResponseTimeMs < FastThresholdMs),
                        Average = snapshot.Count(r => r.ResponseTimeMs >= FastThresholdMs && r.ResponseTimeMs < AverageThresholdMs),
                        Slow = snapshot.Count(r => r.ResponseTimeMs >= AverageThresholdMs)
                    }
                };

                response.Providers.Add(providerStats);
            }

            return response;
        }

        /// <summary>
        /// Gets a performance snapshot for a specific provider
        /// Used for anomaly detection
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <param name="recentWindow">Time window for recent statistics</param>
        /// <returns>Performance statistics</returns>
        public ProviderPerformanceSnapshot GetProviderSnapshot(string providerName, TimeSpan recentWindow)
        {
            if (!records.TryGetValue(providerName, out var providerRecords))
            {
                return new ProviderPerformanceSnapshot { ProviderName = providerName };
            }

            var snapshot = providerRecords.GetSnapshot();

            if (snapshot.Count == 0)
            {
                return new ProviderPerformanceSnapshot { ProviderName = providerName };
            }

            var cutoffTime = DateTime.UtcNow - recentWindow;
            var recentRecords = snapshot.Where(r => r.Timestamp >= cutoffTime).ToList();

            return new ProviderPerformanceSnapshot
            {
                ProviderName = providerName,
                OverallAverageMs = Math.Round(snapshot.Average(r => r.ResponseTimeMs), 2),
                OverallRequestCount = snapshot.Count,
                RecentAverageMs = recentRecords.Count > 0
                    ? Math.Round(recentRecords.Average(r => r.ResponseTimeMs), 2)
                    : null,
                RecentRequestCount = recentRecords.Count
            };
        }

        /// <summary>
        /// Gets all registered provider names who have recorded statistics
        /// </summary>
        /// <returns>Provider names collection</returns>
        public IEnumerable<string> GetProviderNames()
        {
            return [.. records.Keys];
        }

        /// <summary>
        /// Resets all statistics
        /// </summary>
        public void Reset()
        {
            records.Clear();
        }

        /// <summary>
        /// Thread-safe container for provider request records.
        /// Uses lock to ensure atomic read/write operations.
        /// </summary>
        private sealed class ProviderRecords
        {
            private readonly object syncLock = new();
            private readonly List<RequestRecord> items = [];

            public void Add(RequestRecord record)
            {
                lock (syncLock)
                {
                    items.Add(record);
                }
            }

            public List<RequestRecord> GetSnapshot()
            {
                lock (syncLock)
                {
                    return [.. items];
                }
            }
        }
        /// <summary>
        /// local record structure class
        /// </summary>
        private sealed class RequestRecord
        {
            public required double ResponseTimeMs { get; init; }
            public required bool IsSuccess { get; init; }
            public required DateTime Timestamp { get; init; }
        }
    }
}