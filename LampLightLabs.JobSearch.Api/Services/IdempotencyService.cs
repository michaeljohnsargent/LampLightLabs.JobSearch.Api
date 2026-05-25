using System.Collections.Concurrent;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// In-memory idempotency store backed by a ConcurrentDictionary.
    /// Scopes each entry to a (clientId, idempotencyKey) pair so one client's
    /// key cannot collide with another client's identical key.
    ///
    /// In production this would be replaced with a distributed cache such as Redis
    /// so idempotency guarantees hold across multiple API instances.
    /// </summary>
    public class IdempotencyService : IIdempotencyService
    {
        private readonly ConcurrentDictionary<string, IdempotencyCacheEntry> _store = new();

        /// <inheritdoc />
        public bool TryGetCachedResponse(string clientId, string idempotencyKey, out IdempotencyCacheEntry? entry)
        {
            var storeKey = BuildKey(clientId, idempotencyKey);
            return _store.TryGetValue(storeKey, out entry);
        }

        /// <inheritdoc />
        public void StoreResponse(string clientId, string idempotencyKey, string requestHash, int statusCode, object response)
        {
            var storeKey = BuildKey(clientId, idempotencyKey);
            _store[storeKey] = new IdempotencyCacheEntry
            {
                RequestHash = requestHash,
                StatusCode = statusCode,
                Response = response
            };
        }

        /// <summary>
        /// Combines clientId and idempotencyKey into a single dictionary key.
        /// </summary>
        private static string BuildKey(string clientId, string idempotencyKey) =>
            $"{clientId}:{idempotencyKey}";
    }
}
