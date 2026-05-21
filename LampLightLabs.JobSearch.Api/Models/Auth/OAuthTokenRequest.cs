namespace LampLightLabs.JobSearch.Api.Models.Auth
{
    public class OAuthTokenRequest
    {
        /// <summary>
        /// The client ID provided by the OAuth provider.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;
        /// <summary>
        /// The client secret provided by the OAuth provider.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;
        /// <summary>
        /// The grant type for the OAuth token request.
        /// </summary>
        public string GrantType { get; set; } = string.Empty;
        /// <summary>
        /// The scope for the OAuth token request.
        /// </summary>
        public string Scope { get; set; } = string.Empty;
    }
}
