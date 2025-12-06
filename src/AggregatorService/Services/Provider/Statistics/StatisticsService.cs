using AggregatorService.Models.Responses;
using System.Collections.Concurrent;

namespace AggregatorService.Services.Statistics
{
    /// <summary>
    /// Thread-safe in-memory implementation of statistics service
    /// </summary>
    public class StatisticsService : IStatisticsService
    {
        // Thread-safe storage for request records
        private readonly ConcurrentDictionary<string, ConcurrentBag<RequestRecord>> _records = new();

        // Performance bucket thresholds in milliseconds
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

            _records.AddOrUpdate(
                providerName,
                _ => new ConcurrentBag<RequestRecord> { record },
                (_, bag) =>
                {
                    bag.Add(record);
                    return bag;
                });
        }

        public StatisticsResponse GetStatistics()
        {
            var response = new StatisticsResponse();

            foreach (var (providerName, records) in _records)
            {
                var recordsList = records.ToList();

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

        public void Reset()
        {
            _records.Clear();
        }

        /// <summary>
        /// Internal record for storing request data
        /// </summary>
        private class RequestRecord
        {
            public double ResponseTimeMs { get; set; }
            public bool IsSuccess { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}