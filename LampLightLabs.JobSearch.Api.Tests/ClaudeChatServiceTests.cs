using System.Net;
using Anthropic;
using LampLightLabs.JobSearch.Api.Services;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Unit tests for <see cref="ClaudeChatService"/>'s Anthropic-SDK-exception-to-<see cref="AiProviderException"/>
/// translation. A fake <see cref="HttpMessageHandler"/> stands in for the network so these tests
/// exercise the real Anthropic SDK parsing/exception path (status code -> exception type ->
/// ErrorType) rather than re-describing it, which is what let the original billing-error
/// heuristic (keyed off a 403 that Anthropic never actually returns for insufficient credit) ship
/// unnoticed.
/// </summary>
public class ClaudeChatServiceTests
{
    private static ClaudeChatService MakeService(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var client = new AnthropicClient { ApiKey = "test-key", HttpClient = new HttpClient(handler) };
        return new ClaudeChatService(client, "claude-test-model");
    }

    [Fact]
    public async Task SendPromptAsync_400WithBillingErrorType_ThrowsBillingFailure()
    {
        // Arrange: the real shape Anthropic returns for insufficient credit balance —
        // HTTP 400, error.type "billing_error" — per Anthropic's API error documentation.
        var ct = TestContext.Current.CancellationToken;
        var service = MakeService(HttpStatusCode.BadRequest,
            """{"type":"error","error":{"type":"billing_error","message":"Your credit balance is too low to access the Anthropic API."}}""");

        // Act
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => service.SendPromptAsync("hello", ct));

        // Assert
        Assert.Equal(AiProviderFailureReason.Billing, ex.Reason);
        Assert.Equal("Anthropic", ex.Provider);
    }

    [Fact]
    public async Task SendPromptAsync_400WithInvalidRequestErrorType_DoesNotReportBilling()
    {
        // Arrange: a malformed request is also a 400, but must not be misreported as a billing
        // issue — that would send users to "try the demo" for an unrelated bug.
        var ct = TestContext.Current.CancellationToken;
        var service = MakeService(HttpStatusCode.BadRequest,
            """{"type":"error","error":{"type":"invalid_request_error","message":"messages: at least one message is required"}}""");

        // Act
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => service.SendPromptAsync("hello", ct));

        // Assert
        Assert.Equal(AiProviderFailureReason.Unknown, ex.Reason);
    }

    [Fact]
    public async Task SendPromptAsync_403WithoutBillingErrorType_ReportsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = MakeService(HttpStatusCode.Forbidden,
            """{"type":"error","error":{"type":"permission_error","message":"Your API key does not have permission to use this resource."}}""");

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => service.SendPromptAsync("hello", ct));

        Assert.Equal(AiProviderFailureReason.Unauthorized, ex.Reason);
    }

    [Fact]
    public async Task SendPromptAsync_429_ReportsRateLimited()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = MakeService(HttpStatusCode.TooManyRequests,
            """{"type":"error","error":{"type":"rate_limit_error","message":"Rate limit exceeded."}}""");

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => service.SendPromptAsync("hello", ct));

        Assert.Equal(AiProviderFailureReason.RateLimited, ex.Reason);
    }

    [Fact]
    public async Task SendPromptAsync_529_ReportsUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = MakeService((HttpStatusCode)529,
            """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}""");

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => service.SendPromptAsync("hello", ct));

        Assert.Equal(AiProviderFailureReason.Unavailable, ex.Reason);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
