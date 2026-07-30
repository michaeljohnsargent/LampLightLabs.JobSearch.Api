using LampLightLabs.JobSearch.Api.Controllers;
using LampLightLabs.JobSearch.Api.Models.Rag;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    // --- Validation ---

    [Fact]
    public async Task Match_EmptyJobDescription_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var controller = new RagController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "" }, ct);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Match_WhitespaceJobDescription_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var controller = new RagController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "   " }, ct);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Match_EmptyJobDescription_ServiceNotCalled()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        var controller = new RagController(mock.Object);

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
        var controller = new RagController(mock.Object);

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
        var controller = new RagController(mock.Object);

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
        var controller = new RagController(mock.Object);

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
        var controller = new RagController(mock.Object);

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
        var controller = new RagController(mock.Object);

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
        var controller = new RagController(mock.Object);

        await controller.Match(
            new RagMatchRequest { JobDescription = "Requirements\n\n\n\nResponsibilities" }, ct);

        mock.Verify(s => s.MatchAsync("Requirements\n\nResponsibilities", ct), Times.Once);
    }

    // --- Claude API failure mapping ---

    [Theory]
    [InlineData(ClaudeApiFailureReason.Billing)]
    [InlineData(ClaudeApiFailureReason.Unauthorized)]
    [InlineData(ClaudeApiFailureReason.Unavailable)]
    [InlineData(ClaudeApiFailureReason.Unknown)]
    public async Task Match_ClaudeApiUnavailable_Returns503(ClaudeApiFailureReason reason)
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClaudeApiUnavailableException(reason, "Claude API failed."));
        var controller = new RagController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task Match_ClaudeApiRateLimited_Returns429()
    {
        var ct = TestContext.Current.CancellationToken;
        var mock = new Mock<IRagMatchService>();
        mock.Setup(s => s.MatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ClaudeApiUnavailableException(ClaudeApiFailureReason.RateLimited, "Rate limited."));
        var controller = new RagController(mock.Object);

        var result = await controller.Match(new RagMatchRequest { JobDescription = "Senior .NET Engineer" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
    }
}
