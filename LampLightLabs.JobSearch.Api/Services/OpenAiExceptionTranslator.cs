using System.ClientModel;

namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Translates OpenAI SDK failures into <see cref="AiProviderException"/>. Both the embedding
/// generator (<see cref="ResumeVectorStoreService"/>) and the Semantic Kernel OpenAI connector
/// (<see cref="SemanticKernelChatService"/>) are built on the official OpenAI .NET SDK, which
/// raises <see cref="ClientResultException"/> for HTTP-level failures — this is the one place
/// that shape gets translated, so callers never see the OpenAI SDK's raw exception message
/// (which can include quota figures, org identifiers, or billing-page links).
/// </summary>
internal static class OpenAiExceptionTranslator
{
    public static AiProviderException Translate(ClientResultException ex)
    {
        var reason = ex.Status switch
        {
            429 when ex.Message.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) => AiProviderFailureReason.Billing,
            429 => AiProviderFailureReason.RateLimited,
            401 or 403 => AiProviderFailureReason.Unauthorized,
            >= 500 => AiProviderFailureReason.Unavailable,
            _ => AiProviderFailureReason.Unknown
        };

        var message = reason switch
        {
            AiProviderFailureReason.Billing => "The OpenAI account has insufficient quota or another billing issue.",
            AiProviderFailureReason.RateLimited => "The OpenAI API rate limit was exceeded.",
            AiProviderFailureReason.Unauthorized => "The OpenAI API rejected the request as forbidden.",
            AiProviderFailureReason.Unavailable => "The OpenAI API is temporarily unavailable.",
            _ => "The OpenAI API returned an unexpected error."
        };

        return new AiProviderException("OpenAI", reason, message, ex);
    }
}
