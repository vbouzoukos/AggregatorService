using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Aggregation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AggregatorService.Controllers
{
    /// <summary>
    /// Handles data aggregation from multiple external API providers
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AggregationController(IAggregationService aggregationService) : ControllerBase
    {
        /// <summary>
        /// Aggregates data from multiple external API providers based on the provided parameters
        /// </summary>
        /// <param name="request">The aggregation request containing search parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Aggregated response from all applicable providers</returns>
        /// <response code="200">Returns the aggregated data from all providers</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="401">Unauthorized - missing or invalid JWT token</response>
        [HttpPost]
        [ProducesResponseType(typeof(AggregationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AggregationResponse>> Aggregate(
            [FromBody] AggregationRequest request,
            CancellationToken cancellationToken)
        {
            var response = await aggregationService.AggregateAsync(request, cancellationToken);
            return Ok(response);
        }
    }
}