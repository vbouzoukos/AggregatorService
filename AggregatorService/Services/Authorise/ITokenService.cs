using AggregatorService.Models.Requests;
using AggregatorService.Models.Responses;

namespace AggregatorService.Services.Authorise
{
    public interface ITokenService
    {
        AuthenticationResponse GenerateToken(AuthenticationRequest request);
    }

}
