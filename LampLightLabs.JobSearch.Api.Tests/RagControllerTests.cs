using LampLightLabs.JobSearch.Api.Controllers;
using LampLightLabs.JobSearch.Api.Models.Rag;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Unit tests for <see cref="RagController"/>.
///
/// IRagMatchService is mocked throughout, so these tests verify controller
/// logic only (validation, status codes, response shaping) without making
/// any real calls to the vector store or LLM.
/// </summary>
public class RagControllerTests
{
    private static RagController MakeController(IRagMatchService service) =>
        new(service, NullLogger<RagController>.Instance);

    // --- Validation ---

    [Fact]
    public async Task Match_EmptyJobDescription_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "" }, ct);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Match_WhitespaceJobDescription_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "   " }, ct);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Match_EmptyJobDescription_ServiceNotCalled()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var controller = MakeController(mock.Object);

        await controller.Match(new RagMatchRequest { JobDescription = "" }, ct);

        mock.Verify(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Happy path ---

    [Fact]
    public async Task Match_ValidRequest_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagMatchResponse());
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    [Fact]
    public async Task Match_ValidRequest_ReturnsResponseFromService()
    {
        var ct = TestContext.Current.CancellationToken;
        var expected = new RagMatchResponse
        {
            MatchScore = 85,
            Summary = "Strong fit.",
            Strengths = ["C# expertise"],
            Gaps = ["Kubernetes"],
            RetrievedContext = ["Skills chunk"]
        };
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync("Senior .NET Engineer", ct)).ReturnsAsync(expected);
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct) as OkObjectResult;

        Assert.NotNull(result);
        var response = Assert.IsType<RagMatchResponse>(result.Value);
        Assert.Equal(85, response.MatchScore);
        Assert.Equal("Strong fit.", response.Summary);
    }

    // --- Argument passthrough ---

    [Fact]
    public async Task Match_ValidRequest_PassesJobDescriptionToService()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagMatchResponse());
        var controller = MakeController(mock.Object);

        await controller.Match(new RagMatchRequest { JobDescription = "Backend .NET role at Acme Corp" }, ct);

        mock.Verify(s => s.MatchAsync("Backend .NET role at Acme Corp", ct), Times.Once);
    }

    // --- Input sanitization ---

    [Fact]
    public async Task Match_JobDescriptionWithControlCharacters_StripsThemBeforeCallingService()
    {
        // NUL (\x00) and BEL (\x07) are stripped; tabs and double spaces are collapsed to single spaces.
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagMatchResponse());
        var controller = MakeController(mock.Object);

        await controller.Match(
            new RagMatchRequest { JobDescription = "Senior\x00 .NET\x07  Engineer\twith Azure" }, ct);

        mock.Verify(s => s.MatchAsync("Senior .NET Engineer with Azure", ct), Times.Once);
    }

    [Fact]
    public async Task Match_JobDescriptionOnlyControlCharacters_Returns400()
    {
        // A string of only control characters sanitizes to empty — should still be rejected.
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var controller = MakeController(mock.Object);

        var result = await controller.Match(
            new RagMatchRequest { JobDescription = "\x00\x01\x02\x07" }, ct);

        Assert.IsType<BadRequestObjectResult>(result);
        mock.Verify(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Match_JobDescriptionWithExcessiveNewlines_CollapsesToTwoBeforeCallingService()
    {
        // Three or more consecutive newlines are capped at two.
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagMatchResponse());
        var controller = MakeController(mock.Object);

        await controller.Match(
            new RagMatchRequest { JobDescription = "Requirements\n\n\n\nResponsibilities" }, ct);

        mock.Verify(s => s.MatchAsync("Requirements\n\nResponsibilities", ct), Times.Once);
    }

    // --- AI provider failure mapping ---

    [Theory]
    [InlineData(AiProviderFailureReason.Billing)]
    [InlineData(AiProviderFailureReason.Unauthorized)]
    [InlineData(AiProviderFailureReason.Unavailable)]
    [InlineData(AiProviderFailureReason.Unknown)]
    public async Task Match_AiProviderUnavailable_Returns503WithTryDemo(AiProviderFailureReason reason)
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("Anthropic", reason, "Provider failed."));
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task Match_AiProviderRateLimited_Returns429()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("OpenAI", AiProviderFailureReason.RateLimited, "Rate limited."));
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
    }

    [Fact]
    public async Task Match_AiProviderBillingFailure_ResponseDoesNotLeakProviderDetail()
    {
        // The security concern this guards against: SDK exception messages can carry
        // account IDs, exact usage figures, or billing-page links. The response body
        // must contain only the generic client-safe message, never ex.Message.
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var providerMessage = "Org org_abc123 has exceeded its quota. See https://console.anthropic.com/billing for details.";
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("Anthropic", AiProviderFailureReason.Billing, providerMessage));
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsAssignableFrom<object>(objectResult.Value);
        var serialized = System.Text.Json.JsonSerializer.Serialize(body);
        Assert.DoesNotContain("org_abc123", serialized);
        Assert.DoesNotContain("console.anthropic.com", serialized);
        Assert.Contains("temporarily unavailable", serialized);
        Assert.Contains("\"TryDemo\":true", serialized);
    }

    [Fact]
    public async Task Match_UnexpectedException_Returns503WithTryDemoAndNoStackTrace()
    {
        // Safety net beyond the typed AiProviderException — any other unhandled failure
        // (e.g. malformed LLM JSON) must still degrade gracefully, not leak a stack trace.
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM returned an empty or unparseable response."));
        var controller = MakeController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var serialized = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.DoesNotContain("unparseable", serialized);
        Assert.DoesNotContain("StackTrace", serialized);
    }
}
