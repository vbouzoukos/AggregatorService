namespace AggregatorService.Services.Authorise
{
    public interface IIdentityProvider
    {
        void Authenticate(string username, string password);
    }
}
