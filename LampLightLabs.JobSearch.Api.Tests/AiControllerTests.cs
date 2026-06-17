using LampLightLabs.JobSearch.Api.Controllers.V2;
using LampLightLabs.JobSearch.Api.Models.Ai;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
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
    // --- Validation ---

    [Fact]
    public async Task Chat_EmptyPrompt_Returns400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockClaudeChatService = new Mock<IClaudeChatService>();
        var controller = new AiController(mockClaudeChatService.Object);
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
        var controller = new AiController(mockClaudeChatService.Object);
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

        var controller = new AiController(mockClaudeChatService.Object);
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

        var controller = new AiController(mockClaudeChatService.Object);
        var request = new AiChatRequest { Prompt = "Hello, Claude" };

        // Act
        await controller.Chat(request, ct);

        // Assert
        mockClaudeChatService.Verify(s => s.SendPromptAsync("Hello, Claude", ct), Times.Once);
    }
}
