namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Why a call to an upstream AI provider (Anthropic, OpenAI) failed to produce a usable response.
/// </summary>
public enum AiProviderFailureReason
{
    /// <summary>The account has insufficient credits/quota or another billing problem.</summary>
    Billing,

    /// <summary>The request was rejected for a reason other than billing (e.g. the API key lacks model access).</summary>
    Unauthorized,

    /// <summary>The provider's rate limit was exceeded.</summary>
    RateLimited,

    /// <summary>The provider itself is unavailable (5xx, overloaded).</summary>
    Unavailable,

    /// <summary>Any other provider failure not covered above.</summary>
    Unknown
}

/// <summary>
/// Thrown when a call to an upstream AI provider fails, translated from that provider's SDK
/// exception type into an application-level reason so callers (controllers) don't depend on —
/// or accidentally leak details from — the Anthropic or OpenAI SDK's own exception types.
/// </summary>
public class AiProviderException : Exception
{
    /// <summary>Which upstream provider failed, e.g. "Anthropic" or "OpenAI". For logging only — never returned to a client.</summary>
    public string Provider { get; }

    public AiProviderFailureReason Reason { get; }

    public AiProviderException(string provider, AiProviderFailureReason reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Provider = provider;
        Reason = reason;
    }
}
