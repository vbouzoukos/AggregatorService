namespace AggregatorService.Models.Responses
{
    /// <summary>
    /// Response containing aggregated data from multiple external API providers
    /// </summary>
    public class AggregationResponse
    {
        /// <summary>
        /// Timestamp when the aggregation was performed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total time taken to aggregate all responses
        /// </summary>
        public TimeSpan TotalResponseTime { get; set; }

        /// <summary>
        /// Number of providers that were called
        /// </summary>
        public int ProvidersQueried { get; set; }

        /// <summary>
        /// Number of providers that returned successful responses
        /// </summary>
        public int SuccessfulResponses { get; set; }

        /// <summary>
        /// Individual responses from each provider
        /// </summary>
        public List<ApiResponse> Results { get; set; } = [];
    }
}