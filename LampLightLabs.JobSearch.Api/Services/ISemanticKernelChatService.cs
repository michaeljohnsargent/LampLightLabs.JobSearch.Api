namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Defines the contract for sending chat prompts to an OpenAI model through
/// Microsoft Semantic Kernel.
/// </summary>
public interface ISemanticKernelChatService
{
    /// <summary>
    /// Sends a prompt to the configured OpenAI model via Semantic Kernel and
    /// returns the text of its response.
    /// </summary>
    /// <param name="prompt">The user-supplied prompt.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The model's text response.</returns>
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken);
}
