using Anthropic;
using Anthropic.Models.Messages;

namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Sends chat prompts to the Anthropic Claude API using the official Anthropic .NET SDK.
/// </summary>
public class ClaudeChatService : IClaudeChatService
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    /// <summary>
    /// Initializes a new instance of <see cref="ClaudeChatService"/>.
    /// </summary>
    /// <param name="configuration">Application configuration for Anthropic settings.</param>
    public ClaudeChatService(IConfiguration configuration)
    {
        var apiKey = configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic API key is not configured. Use user secrets or environment variables in production.");

        _client = new AnthropicClient { ApiKey = apiKey };
        _model = configuration["Anthropic:Model"] ?? "claude-opus-4-8";
    }

    /// <inheritdoc />
    public async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            Messages = [new() { Role = Role.User, Content = prompt }]
        }, cancellationToken: cancellationToken);

        return ExtractText(response);
    }

    /// <inheritdoc />
    public async Task<string> SendPromptAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            System = systemPrompt,
            Messages = [new() { Role = Role.User, Content = userMessage }]
        }, cancellationToken: cancellationToken);

        return ExtractText(response);
    }

    private static string ExtractText(Message response) =>
        response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault() ?? string.Empty;
}
