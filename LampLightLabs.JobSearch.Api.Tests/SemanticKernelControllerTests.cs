using LampLightLabs.JobSearch.Api.Controllers.V2;
using LampLightLabs.JobSearch.Api.Models.Sk;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Unit tests for <see cref="SemanticKernelController"/>.
///
/// ISemanticKernelChatService is mocked throughout, so these tests verify the
/// controller's own logic (prompt validation, status codes, response
/// shaping) without making real calls to OpenAI or building a Semantic
/// Kernel <see cref="Microsoft.SemanticKernel.Kernel"/> instance.
/// </summary>
public class SemanticKernelControllerTests
{
    // --- Validation ---

    [Fact]
    public async Task Chat_EmptyPrompt_Returns400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        var controller = new SemanticKernelController(mockSemanticKernelChatService.Object);
        var request = new SkChatRequest { Prompt = "" };

        // Act
        var result = await controller.Chat(request, ct);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        mockSemanticKernelChatService.Verify(
            s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Chat_WhitespacePrompt_Returns400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        var controller = new SemanticKernelController(mockSemanticKernelChatService.Object);
        var request = new SkChatRequest { Prompt = "   " };

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
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        mockSemanticKernelChatService
            .Setup(s => s.SendPromptAsync("What is the capital of France?", ct))
            .ReturnsAsync("Paris is the capital of France.");

        var controller = new SemanticKernelController(mockSemanticKernelChatService.Object);
        var request = new SkChatRequest { Prompt = "What is the capital of France?" };

        // Act
        var result = await controller.Chat(request, ct) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var response = result.Value as SkChatResponse;
        Assert.NotNull(response);
        Assert.Equal("Paris is the capital of France.", response.Response);
    }

    // --- Argument passthrough ---

    [Fact]
    public async Task Chat_ValidPrompt_PassesPromptToService()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        mockSemanticKernelChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        var controller = new SemanticKernelController(mockSemanticKernelChatService.Object);
        var request = new SkChatRequest { Prompt = "Hello, Semantic Kernel" };

        // Act
        await controller.Chat(request, ct);

        // Assert
        mockSemanticKernelChatService.Verify(s => s.SendPromptAsync("Hello, Semantic Kernel", ct), Times.Once);
    }
}
