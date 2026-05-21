namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Defines the contract for OAuth 2.0 client credential validation.
/// </summary>
public interface IOAuthClientService
{
    /// <summary>
    /// Validates a client ID and secret against the registered client store.
    /// </summary>
    bool ValidateClient(string clientId, string clientSecret);
}