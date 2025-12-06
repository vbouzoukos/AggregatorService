using AggregatorService.Models.Responses;
using AggregatorService.Services.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AggregatorService.Controllers
{
    /// <summary>
    /// Handles retrieval of API request statistics
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController(IStatisticsService statisticsService) : ControllerBase
    {
        /// <summary>
        /// Retrieves request statistics for all API providers
        /// </summary>
        /// <returns>Statistics grouped by provider with performance buckets</returns>
        /// <response code="200">Returns the statistics for all providers</response>
        /// <response code="401">Unauthorized - missing or invalid JWT token</response>
        [HttpGet]
        [ProducesResponseType(typeof(StatisticsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<StatisticsResponse> GetStatistics()
        {
            var response = statisticsService.GetStatistics();
            return Ok(response);
        }

        /// <summary>
        /// Resets all statistics data
        /// </summary>
        /// <returns>No content on success</returns>
        /// <response code="204">Statistics reset successfully</response>
        /// <response code="401">Unauthorized - missing or invalid JWT token</response>
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