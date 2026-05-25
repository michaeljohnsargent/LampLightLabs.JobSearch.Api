namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Provides idempotency guarantees for write operations by caching responses
    /// keyed to a client identity and a caller-supplied idempotency key.
    /// </summary>
    public interface IIdempotencyService
    {
        /// <summary>
        /// Attempts to retrieve a previously cached response for the given client and key.
        /// </summary>
        /// <param name="clientId">The authenticated client identity.</param>
        /// <param name="idempotencyKey">The caller-supplied idempotency key.</param>
        /// <param name="entry">The cached entry if found; otherwise null.</param>
        /// <returns>True if a cached entry exists; otherwise false.</returns>
        bool TryGetCachedResponse(string clientId, string idempotencyKey, out IdempotencyCacheEntry? entry);

        /// <summary>
        /// Stores a response in the cache for the given client and key.
        /// </summary>
        /// <param name="clientId">The authenticated client identity.</param>
        /// <param name="idempotencyKey">The caller-supplied idempotency key.</param>
        /// <param name="requestHash">SHA-256 hash of the original request body.</param>
        /// <param name="statusCode">HTTP status code of the response.</param>
        /// <param name="response">Response body to cache.</param>
        void StoreResponse(string clientId, string idempotencyKey, string requestHash, int statusCode, object response);
    }
}
