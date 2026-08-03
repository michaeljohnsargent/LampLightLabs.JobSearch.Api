using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models;
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

    // Test-only seam: lets tests supply an AnthropicClient wired to a fake HttpClient so the
    // real SDK exception-translation path (status code -> exception type -> ErrorType) can be
    // exercised without a network call.
    internal ClaudeChatService(AnthropicClient client, string model)
    {
        _client = client;
        _model = model;
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

    // Translates Anthropic SDK exceptions into AiProviderException so callers depend on an
    // application-level failure reason rather than the Anthropic SDK's exception hierarchy —
    // and never see the SDK's raw exception message, which can include account/billing detail.
    //
    // Insufficient credit balance comes back as HTTP 400 (AnthropicBadRequestException) with
    // error.type "billing_error" in the response body, not HTTP 403 as you might guess from the
    // name "billing". The SDK parses that body for every AnthropicApiException subtype and
    // exposes it as the ErrorType property (see AnthropicExceptionFactory.ExtractErrorType), so
    // matching on ex.ErrorType here catches the real failure regardless of which HTTP status
    // Anthropic pairs it with, and won't misclassify an unrelated 400 (e.g. a malformed request,
    // which comes back as error.type "invalid_request_error") as a billing issue.
    private async Task<Message> CreateMessageAsync(MessageCreateParams parameters, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.Messages.Create(parameters, cancellationToken: cancellationToken);
        }
        catch (AnthropicApiException ex) when (ex.ErrorType == ErrorType.BillingError)
        {
            throw new AiProviderException(
                "Anthropic",
                AiProviderFailureReason.Billing,
                "The Claude API account has insufficient credits or another billing issue.",
                ex);
        }
        catch (AnthropicForbiddenException ex)
        {
            throw new AiProviderException(
                "Anthropic",
                AiProviderFailureReason.Unauthorized,
                "The Claude API rejected the request as forbidden.",
                ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new AiProviderException(
                "Anthropic",
                AiProviderFailureReason.RateLimited,
                "The Claude API rate limit was exceeded.",
                ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new AiProviderException(
                "Anthropic",
                AiProviderFailureReason.Unavailable,
                "The Claude API is temporarily unavailable.",
                ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new AiProviderException(
                "Anthropic",
                AiProviderFailureReason.Unknown,
                "The Claude API returned an unexpected error.",
                ex);
        }
    }

    private static string ExtractText(Message response) =>
        response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault() ?? string.Empty;
}
