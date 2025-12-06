namespace AggregatorService.Models.Responses
{
    /// <summary>
    /// Response containing statistics for all API providers
    /// </summary>
    public class StatisticsResponse
    {
        /// <summary>
        /// Timestamp when statistics were retrieved
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Statistics for each provider
        /// </summary>
        public List<ProviderStatistics> Providers { get; set; } = [];
    }
}