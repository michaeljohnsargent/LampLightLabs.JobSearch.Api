namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Defines the contract for sending chat prompts to the Anthropic Claude API.
/// </summary>
public interface IClaudeChatService
{
    /// <summary>
    /// Sends a prompt to Claude and returns the text of its response.
    /// </summary>
    /// <param name="prompt">The user-supplied prompt.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>Claude's text response.</returns>
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken);
}
