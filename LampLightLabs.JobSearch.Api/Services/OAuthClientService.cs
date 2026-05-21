namespace LampLightLabs.JobSearch.Api.Services;

public class OAuthClientService : IOAuthClientService
{
    private readonly IConfiguration _config;

    public OAuthClientService(IConfiguration config)
    {
        _config = config;
    }

    public bool ValidateClient(string clientId, string clientSecret)
    {
        var clients = _config.GetSection("OAuthClients")
            .Get<List<OAuthClientConfig>>();

        return clients != null &&
               clients.Any(c => c.ClientId == clientId &&
                                c.ClientSecret == clientSecret);
    }
}

public class OAuthClientConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public List<string> AllowedScopes { get; set; } = new();
}