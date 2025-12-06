using AggregatorService.Models.Responses;
using System.Collections.Concurrent;

namespace AggregatorService.Services.Statistics
{
    /// <summary>
    /// Thread-safe in-memory implementation of statistics service
    /// </summary>
    public class StatisticsService : IStatisticsService
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<RequestRecord>> records = new();

        private const int FastThresholdMs = 100;
        private const int AverageThresholdMs = 200;

        public void RecordRequest(string providerName, TimeSpan responseTime, bool isSuccess)
        {
            var record = new RequestRecord
            {
                ResponseTimeMs = responseTime.TotalMilliseconds,
                IsSuccess = isSuccess,
                Timestamp = DateTime.UtcNow
            };

            records.AddOrUpdate(providerName, _ => [record], (_, bag) =>
            {
                bag.Add(record);
                return bag;
            });
        }

        public StatisticsResponse GetStatistics()
        {
            var response = new StatisticsResponse();

            foreach (var (providerName, precs) in records)
            {
                var recordsList = precs.ToList();

                if (recordsList.Count == 0)
                    continue;

                var providerStats = new ProviderStatistics
                {
                    ProviderName = providerName,
                    TotalRequests = recordsList.Count,
                    SuccessfulRequests = recordsList.Count(r => r.IsSuccess),
                    FailedRequests = recordsList.Count(r => !r.IsSuccess),
                    AverageResponseTimeMs = Math.Round(recordsList.Average(r => r.ResponseTimeMs), 2),
                    PerformanceBuckets = new PerformanceBuckets
                    {
                        Fast = recordsList.Count(r => r.ResponseTimeMs < FastThresholdMs),
                        Average = recordsList.Count(r => r.ResponseTimeMs >= FastThresholdMs && r.ResponseTimeMs < AverageThresholdMs),
                        Slow = recordsList.Count(r => r.ResponseTimeMs >= AverageThresholdMs)
                    }
                };

                response.Providers.Add(providerStats);
            }

            return response;
        }

        public ProviderPerformanceSnapshot GetProviderSnapshot(string providerName, TimeSpan recentWindow)
        {
            if (!records.TryGetValue(providerName, out var providerRecords))
            {
                return new ProviderPerformanceSnapshot { ProviderName = providerName };
            }

            var recordsList = providerRecords.ToList();
            if (recordsList.Count == 0)
            {
                return new ProviderPerformanceSnapshot { ProviderName = providerName };
            }

            var cutoffTime = DateTime.UtcNow - recentWindow;
            var recentRecords = recordsList.Where(r => r.Timestamp >= cutoffTime).ToList();

            return new ProviderPerformanceSnapshot
            {
                ProviderName = providerName,
                OverallAverageMs = Math.Round(recordsList.Average(r => r.ResponseTimeMs), 2),
                OverallRequestCount = recordsList.Count,
                RecentAverageMs = recentRecords.Count > 0
                    ? Math.Round(recentRecords.Average(r => r.ResponseTimeMs), 2)
                    : null,
                RecentRequestCount = recentRecords.Count
            };
        }

        public IEnumerable<string> GetProviderNames()
        {
            return [.. records.Keys];
        }

        public void Reset()
        {
            records.Clear();
        }

        private sealed class RequestRecord
        {
            public double ResponseTimeMs { get; set; }
            public bool IsSuccess { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }

}