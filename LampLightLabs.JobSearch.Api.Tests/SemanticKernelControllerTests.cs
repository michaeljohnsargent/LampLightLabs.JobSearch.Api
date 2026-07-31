using LampLightLabs.JobSearch.Api.Controllers.V2;
using LampLightLabs.JobSearch.Api.Models.Sk;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
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
    private static SemanticKernelController MakeController(ISemanticKernelChatService service) =>
        new(service, NullLogger<SemanticKernelController>.Instance);

    // --- Validation ---

    [Fact]
    public async Task Chat_EmptyPrompt_Returns400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        var controller = MakeController(mockSemanticKernelChatService.Object);
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
        var controller = MakeController(mockSemanticKernelChatService.Object);
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

        var controller = MakeController(mockSemanticKernelChatService.Object);
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

        var controller = MakeController(mockSemanticKernelChatService.Object);
        var request = new SkChatRequest { Prompt = "Hello, Semantic Kernel" };

        // Act
        await controller.Chat(request, ct);

        // Assert
        mockSemanticKernelChatService.Verify(s => s.SendPromptAsync("Hello, Semantic Kernel", ct), Times.Once);
    }

    // --- AI provider failure mapping ---

    [Fact]
    public async Task Chat_ProviderBillingFailure_Returns503WithoutLeakingProviderDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        var providerMessage = "You exceeded your current quota, please check your plan and billing details.";
        mockSemanticKernelChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("OpenAI", AiProviderFailureReason.Billing, providerMessage));
        var controller = MakeController(mockSemanticKernelChatService.Object);

        var result = await controller.Chat(new SkChatRequest { Prompt = "Hello, Semantic Kernel" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var serialized = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.DoesNotContain("billing details", serialized);
    }

    [Fact]
    public async Task Chat_ProviderRateLimited_Returns429()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        mockSemanticKernelChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderException("OpenAI", AiProviderFailureReason.RateLimited, "Rate limited."));
        var controller = MakeController(mockSemanticKernelChatService.Object);

        var result = await controller.Chat(new SkChatRequest { Prompt = "Hello, Semantic Kernel" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
    }

    [Fact]
    public async Task Chat_UnexpectedException_Returns503WithoutStackTrace()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockSemanticKernelChatService = new Mock<ISemanticKernelChatService>();
        mockSemanticKernelChatService
            .Setup(s => s.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something internal broke at C:\\secret\\path.cs:42"));
        var controller = MakeController(mockSemanticKernelChatService.Object);

        var result = await controller.Chat(new SkChatRequest { Prompt = "Hello, Semantic Kernel" }, ct);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var serialized = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.DoesNotContain("secret", serialized);
        Assert.DoesNotContain("StackTrace", serialized);
    }
}
