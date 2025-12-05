namespace AggregatorService.Services.Authorise
{
    public class IdentityProvider : IIdentityProvider
    {
        // In-memory user store for this demo
        private static readonly Dictionary<string, string> Users = new()
        {
            { "admin", "admin123" },
            { "user", "user123" },
            { "demo", "demo123" }
        };
        public void Authenticate(string username, string password)
        {
            // Validate credentials against user store (in a real scenario it should be a repository or an identity providere)
            if (!Users.TryGetValue(username, out var pwd) || password != pwd)
            {
                throw new UnauthorizedAccessException("Invalid username or password");
            }
        }
    }
}
