namespace LampLightLabs.JobSearch.Api.Models.Auth
{
    public class OAuthTokenResponse
    {
        /// <summary>
        /// The access token issued by the OAuth provider.
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;
        /// <summary>
        /// The type of the token (e.g., "Bearer").
        /// </summary>
        public string TokenType { get; set; } = string.Empty;
        /// <summary>
        /// The duration in seconds for which the access token is valid.
        /// </summary>
        public int ExpiresIn { get; set; }
        /// <summary>
        /// The scope of the access token issued by the OAuth provider.
        /// </summary>
        public string Scope { get; set; } = string.Empty;
    }
}
