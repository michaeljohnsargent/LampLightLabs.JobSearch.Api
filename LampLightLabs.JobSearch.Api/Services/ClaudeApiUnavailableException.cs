namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Why a call to the Claude API failed to produce a usable response.
/// </summary>
public enum ClaudeApiFailureReason
{
    /// <summary>The account has insufficient credits or another billing problem (HTTP 403, error type "billing_error").</summary>
    Billing,

    /// <summary>The request was rejected as forbidden for a reason other than billing (e.g. the API key lacks model access).</summary>
    Unauthorized,

    /// <summary>The Claude API rate limit was exceeded (HTTP 429).</summary>
    RateLimited,

    /// <summary>The Claude API itself is unavailable (HTTP 5xx or overloaded).</summary>
    Unavailable,

    /// <summary>Any other Claude API failure not covered above.</summary>
    Unknown
}

/// <summary>
/// Thrown by <see cref="ClaudeChatService"/> when the Anthropic .NET SDK reports a failure calling
/// the Claude API, translated into an application-level reason so callers (e.g. <c>RagController</c>)
/// can respond appropriately without depending on the Anthropic SDK's exception types directly.
/// </summary>
public class ClaudeApiUnavailableException : Exception
{
    public ClaudeApiFailureReason Reason { get; }

    /// <summary>How long the caller should wait before retrying, if the Claude API provided one (typically only set for <see cref="ClaudeApiFailureReason.RateLimited"/>).</summary>
    public TimeSpan? RetryAfter { get; }

    public ClaudeApiUnavailableException(ClaudeApiFailureReason reason, string message, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        RetryAfter = retryAfter;
    }
}
