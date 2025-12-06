using AggregatorService.Models.Responses;
using AggregatorService.Services.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AggregatorService.Controllers
{
    /// <summary>
    /// Handles retrieval of API request statistics and performance monitoring
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController(
        IStatisticsService statisticsService,
        IConfiguration configuration) : ControllerBase
    {
        private const string ConfigKey = "PerformanceMonitor";

        /// <summary>
        /// Retrieves request statistics for all API providers
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(StatisticsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<StatisticsResponse> GetStatistics()
        {
            var response = statisticsService.GetStatistics();
            return Ok(response);
        }

        /// <summary>
        /// Retrieves current performance status and anomaly detection results
        /// </summary>
        [HttpGet("performance")]
        [ProducesResponseType(typeof(PerformanceAnomalyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<PerformanceAnomalyResponse> GetPerformanceStatus()
        {
            var recentWindowMinutes = configuration.GetValue($"{ConfigKey}:RecentWindowMinutes", 5);
            var anomalyThresholdPercent = configuration.GetValue($"{ConfigKey}:AnomalyThresholdPercent", 50);
            var recentWindow = TimeSpan.FromMinutes(recentWindowMinutes);

            var response = new PerformanceAnomalyResponse
            {
                RecentWindowMinutes = recentWindowMinutes,
                AnomalyThresholdPercent = anomalyThresholdPercent
            };

            var providerNames = statisticsService.GetProviderNames();

            foreach (var providerName in providerNames)
            {
                var snapshot = statisticsService.GetProviderSnapshot(providerName, recentWindow);

                var status = new ProviderPerformanceStatus
                {
                    ProviderName = snapshot.ProviderName,
                    OverallAverageMs = snapshot.OverallAverageMs,
                    OverallRequestCount = snapshot.OverallRequestCount,
                    RecentAverageMs = snapshot.RecentAverageMs,
                    RecentRequestCount = snapshot.RecentRequestCount
                };

                // Calculate degradation if we have recent data
                if (snapshot.RecentAverageMs.HasValue && snapshot.OverallAverageMs > 0)
                {
                    var degradation = ((snapshot.RecentAverageMs.Value - snapshot.OverallAverageMs) / snapshot.OverallAverageMs) * 100;
                    status.DegradationPercent = Math.Round(degradation, 1);
                    status.IsAnomaly = degradation > anomalyThresholdPercent;
                    status.Status = status.IsAnomaly ? "Anomaly" : "Normal";

                    if (snapshot.RecentRequestCount < 2 || snapshot.OverallRequestCount < 5)
                    {
                        status.Status = "Insufficient Data";
                        status.IsAnomaly = false;
                    }
                }
                else
                {
                    status.Status = "No Recent Data";
                }

                response.Providers.Add(status);
            }

            return Ok(response);
        }

        /// <summary>
        /// Resets all statistics data
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult ResetStatistics()
        {
            statisticsService.Reset();
            return NoContent();
        }
    }
}