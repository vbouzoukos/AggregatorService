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
        /// <param name="providerName">Name of the provider</param>
        /// <param name="responseTime">Time taken for the request</param>
        /// <param name="isSuccess">Whether the request was successful</param>
        void RecordRequest(string providerName, TimeSpan responseTime, bool isSuccess);

        /// <summary>
        /// Gets statistics for all providers
        /// </summary>
        /// <returns>Statistics grouped by provider</returns>
        StatisticsResponse GetStatistics();

        /// <summary>
        /// Resets all statistics
        /// </summary>
        void Reset();
    }
}