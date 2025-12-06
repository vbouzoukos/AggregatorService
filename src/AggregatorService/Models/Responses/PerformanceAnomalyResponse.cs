namespace AggregatorService.Models.Responses
{
    /// <summary>
    /// Response model for performance anomaly data
    /// </summary>
    public class PerformanceAnomalyResponse
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int RecentWindowMinutes { get; set; }
        public int AnomalyThresholdPercent { get; set; }
        public List<ProviderPerformanceStatus> Providers { get; set; } = [];
    }

    /// <summary>
    /// Performance status for a single provider
    /// </summary>
    public class ProviderPerformanceStatus
    {
        public string ProviderName { get; set; } = string.Empty;
        public double OverallAverageMs { get; set; }
        public int OverallRequestCount { get; set; }
        public double? RecentAverageMs { get; set; }
        public int RecentRequestCount { get; set; }
        public double? DegradationPercent { get; set; }
        public bool IsAnomaly { get; set; }
        public string Status { get; set; } = "Normal";
    }
}