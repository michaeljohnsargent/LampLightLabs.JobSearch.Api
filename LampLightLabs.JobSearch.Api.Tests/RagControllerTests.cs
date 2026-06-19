using LampLightLabs.JobSearch.Api.Controllers;
using LampLightLabs.JobSearch.Api.Models.Rag;
using LampLightLabs.JobSearch.Api.Services;
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
}
