namespace LampLightLabs.JobSearch.Api.Models.Auth;

/// <summary>
/// Response model containing the generated JWT bearer token.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// The signed JWT bearer token.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token expiry timestamp in UTC.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
