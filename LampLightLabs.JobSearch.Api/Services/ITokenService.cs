namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Defines the contract for JWT token generation.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a signed JWT bearer token for the specified username.
    /// </summary>
    /// <param name="username">The authenticated username.</param>
    /// <returns>A signed JWT token string.</returns>
    string GenerateToken(string username);
}
