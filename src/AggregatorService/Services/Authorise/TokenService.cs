using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;
using Microsoft.IdentityModel.Tokens;

namespace AggregatorService.Services.Authorise
{
    /// <summary>
    /// Service responsible for generating JWT tokens for authenticated users
    /// </summary>
    public class TokenService(IConfiguration configuration, IIdentityProvider identityProvider) : ITokenService
    {

        /// <summary>
        /// Validates user credentials and generates a JWT token
        /// </summary>
        /// <param name="request">Authentication request containing username and password</param>
        /// <returns>Authentication response containing the JWT token and expiration</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when credentials are invalid</exception>
        public AuthenticationResponse GenerateToken(AuthenticationRequest request)
        {
            // Since we have a demo identity provider we do not need a return type it simply throws UnauthorizedAccessException
            // In a real scenario it would be async call
            identityProvider.Authenticate(request.Username, request.Password);
            // Retrieve JWT settings from configuration
            var secretKey = configuration["JwtSettings:SecretKey"];
            var issuer = configuration["JwtSettings:Issuer"];
            var audience = configuration["JwtSettings:Audience"];
            var expirationMinutes = int.Parse(configuration["JwtSettings:ExpirationInMinutes"] ?? "60");

            // Create signing credentials using the secret key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Define claims to be included in the token
            // for simplicity we include the username
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

            // Generate the JWT token
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            return new AuthenticationResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expiresAt
            };
        }
    }
}