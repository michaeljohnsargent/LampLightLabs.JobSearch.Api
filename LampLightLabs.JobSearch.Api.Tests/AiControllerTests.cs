using LampLightLabs.JobSearch.Api.Controllers.V2;
using LampLightLabs.JobSearch.Api.Models.Ai;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Unit tests for <see cref="AiController"/>.
///
/// IClaudeChatService is mocked throughout, so these tests verify the
/// controller's own logic (prompt validation, status codes, response
/// shaping) without making real calls to the Anthropic Claude API.
/// </summary>
public class AiControllerTests
{
    private static AiController MakeController(IClaudeChatService service) =>
        new(service, NullLogger<AiController>.Instance);

    // --- Validation ---

    [Fact]
    public async Task Chat_EmptyPrompt_Returns400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        var controller = MakeController(mockClaudeChatService.Object);
        var request = new AiChatRequest { Prompt = "" };

        // Act
        var result = await controller.Chat(request, ct);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        mockClaudeChatService.Verify(
            s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_WhitespacePrompt_Returns400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        var controller = MakeController(mockClaudeChatService.Object);
        var request = new AiChatRequest { Prompt = "   " };

        // Act
        var result = await controller.Chat(request, ct);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- Happy path ---

    [Fact]
    public async Task Chat_ValidPrompt_Returns200WithResponse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        mockClaudeChatService
            .Setup(s => s.SendPromptAsync("What is the capital of France?", ct))
            .ReturnsAsync("Paris is the capital of France.");

        var controller = MakeController(mockClaudeChatService.Object);
        var request = new AiChatRequest { Prompt = "What is the capital of France?" };

        // Act
        var result = await controller.Chat(request, ct) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var response = result.Value as AiChatResponse;
        Assert.NotNull(response);
        Assert.Equal("Paris is the capital of France.", response.Response);
    }

    // --- Argument passthrough ---

    [Fact]
    public async Task Chat_ValidPrompt_PassesPromptToService()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        mockClaudeChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        var controller = MakeController(mockClaudeChatService.Object);
        var request = new AiChatRequest { Prompt = "Hello, Claude" };

        // Act
        await controller.Chat(request, ct);

        // Assert
        mockClaudeChatService.Verify(s => s.SendPromptAsync("Hello, Claude", ct), Times.Once);
    }

    // --- AI provider failure mapping ---

    [Fact]
    public async Task Chat_ProviderBillingFailure_Returns503WithoutLeakingProviderDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        var providerMessage = "Org org_abc123 has exceeded its quota. See https://console.anthropic.com/billing for details.";
        mockClaudeChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("Anthropic", AiProviderFailureReason.Billing, providerMessage));
        var controller = MakeController(mockClaudeChatService.Object);

        var result = await controller.Chat(new AiChatRequest { Prompt = "Hello, Claude" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var serialized = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.DoesNotContain("org_abc123", serialized);
        Assert.DoesNotContain("console.anthropic.com", serialized);
    }

    [Fact]
    public async Task Chat_ProviderRateLimited_Returns429()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        mockClaudeChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("Anthropic", AiProviderFailureReason.RateLimited, "Rate limited."));
        var controller = MakeController(mockClaudeChatService.Object);

        var result = await controller.Chat(new AiChatRequest { Prompt = "Hello, Claude" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
    }

    [Fact]
    public async Task Chat_UnexpectedException_Returns503WithoutStackTrace()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        mockClaudeChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something internal broke at C:\\secret\\path.cs:42"));
        var controller = MakeController(mockClaudeChatService.Object);

        var result = await controller.Chat(new AiChatRequest { Prompt = "Hello, Claude" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var serialized = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.DoesNotContain("secret", serialized);
        Assert.DoesNotContain("StackTrace", serialized);
    }
}
