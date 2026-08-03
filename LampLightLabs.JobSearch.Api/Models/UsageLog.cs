namespace LampLightLabs.JobSearch.Api.Models
{
    /// <summary>
    /// Records one real (non-demo, non-cached) call to a metered AI endpoint, so spend can be
    /// summed per calendar month against the budget/ceiling in the <c>UsageTracking</c> config
    /// section. Only written when the real pipeline actually runs — never for demo/short-circuited
    /// responses, and never for cache hits (match-result caching isn't shipped yet, but the intent
    /// is that a cache hit incurs no cost and so isn't usage).
    /// </summary>
    public class UsageLog
    {
        /// <summary>Identity primary key.</summary>
        public int Id { get; set; }

        /// <summary>UTC timestamp when the real pipeline call was made.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>The endpoint that incurred the cost, e.g. "api/rag/match".</summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>Flat estimated cost of the call, from <c>UsageTracking:EstimatedCostPerCallUsd</c>.</summary>
        public decimal EstimatedCostUsd { get; set; }
    }
}
