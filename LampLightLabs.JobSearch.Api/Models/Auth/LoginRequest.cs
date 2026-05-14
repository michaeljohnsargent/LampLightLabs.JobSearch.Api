namespace LampLightLabs.JobSearch.Api.Models.Auth;

/// <summary>
/// Request model for JWT authentication.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// The username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The password.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
