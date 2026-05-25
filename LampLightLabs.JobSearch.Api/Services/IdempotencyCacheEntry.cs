namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Represents a cached response stored by the idempotency service.
    /// Holds the original request fingerprint, HTTP status code, and response body
    /// so identical retries can be replayed without re-executing the operation.
    /// </summary>
    public class IdempotencyCacheEntry
    {
        /// <summary>
        /// SHA-256 hash of the original serialized request body.
        /// Used to detect key reuse on a different payload.
        /// </summary>
        public string RequestHash { get; set; } = string.Empty;

        /// <summary>
        /// HTTP status code of the original response.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// The response body returned on the original call.
        /// </summary>
        public object Response { get; set; } = new();

        /// <summary>
        /// UTC timestamp when this entry was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
