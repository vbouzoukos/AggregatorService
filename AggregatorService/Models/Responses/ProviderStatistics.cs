namespace AggregatorService.Models.Responses
{
    /// <summary>
    /// Statistics for a single provider
    /// </summary>
    public class ProviderStatistics
    {
        /// <summary>
        /// Name of the provider
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Total number of requests made
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Number of successful requests
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Number of failed requests
        /// </summary>
        public int FailedRequests { get; set; }

        /// <summary>
        /// Average response time across all requests
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Performance breakdown by bucket
        /// </summary>
        public PerformanceBuckets PerformanceBuckets { get; set; } = new();
    }
}