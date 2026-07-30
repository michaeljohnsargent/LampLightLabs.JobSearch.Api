using Anthropic;
using Anthropic.Exceptions;
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
        var response = await CreateMessageAsync(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            Messages = [new() { Role = Role.User, Content = prompt }]
        }, cancellationToken);

        return ExtractText(response);
    }

    /// <inheritdoc />
    public async Task<string> SendPromptAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken)
    {
        var response = await CreateMessageAsync(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 1024,
            System = systemPrompt,
            Messages = [new() { Role = Role.User, Content = userMessage }]
        }, cancellationToken);

        return ExtractText(response);
    }

    // Translates Anthropic SDK exceptions into ClaudeApiUnavailableException so callers depend on
    // an application-level failure reason rather than the Anthropic SDK's exception hierarchy directly.
    private async Task<Message> CreateMessageAsync(MessageCreateParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.Messages.Create(parameters, cancellationToken: cancellationToken);
        }
        catch (AnthropicForbiddenException ex) when (ex.Message.Contains("billing_error", StringComparison.OrdinalIgnoreCase))
        {
            throw new ClaudeApiUnavailableException(
                ClaudeApiFailureReason.Billing,
                "The Claude API account has insufficient credits or another billing issue.",
                innerException: ex);
        }
        catch (AnthropicForbiddenException ex)
        {
            throw new ClaudeApiUnavailableException(
                ClaudeApiFailureReason.Unauthorized,
                "The Claude API rejected the request as forbidden.",
                innerException: ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            // The SDK doesn't expose a typed Retry-After value on this exception, so RetryAfter
            // is left unset here; callers should apply their own backoff.
            throw new ClaudeApiUnavailableException(
                ClaudeApiFailureReason.RateLimited,
                "The Claude API rate limit was exceeded.",
                innerException: ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new ClaudeApiUnavailableException(
                ClaudeApiFailureReason.Unavailable,
                "The Claude API is temporarily unavailable.",
                innerException: ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new ClaudeApiUnavailableException(
                ClaudeApiFailureReason.Unknown,
                "The Claude API returned an unexpected error.",
                innerException: ex);
        }
    }

    private static string ExtractText(Message response) =>
        response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault() ?? string.Empty;
}
