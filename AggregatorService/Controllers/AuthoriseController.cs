using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using AggregatorService.Services.Authorise;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AggregatorService.Controllers
{
    /// <summary>
    /// Handles authentication operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthoriseController(ITokenService tokenService) : ControllerBase
    {
        /// <summary>
        /// Authenticates a user and returns a JWT token
        /// </summary>
        /// <param name="request">The authentication credentials</param>
        /// <returns>JWT token with expiration details</returns>
        /// <response code="200">Returns the JWT token</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="401">Invalid credentials</response>
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthenticationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<AuthenticationResponse> Login([FromBody] AuthenticationRequest request)
        {
            var response = tokenService.GenerateToken(request);
            return Ok(response);
        }
    }
}