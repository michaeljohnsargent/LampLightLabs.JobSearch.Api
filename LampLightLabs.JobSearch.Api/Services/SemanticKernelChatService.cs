using System.ClientModel;
using Microsoft.SemanticKernel;

namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Sends chat prompts to an OpenAI chat completion model using Microsoft
/// Semantic Kernel's OpenAI connector.
/// </summary>
public class SemanticKernelChatService : ISemanticKernelChatService
{
    private readonly Kernel _kernel;

    /// <summary>
    /// Initializes a new instance of <see cref="SemanticKernelChatService"/>,
    /// building a Semantic Kernel <see cref="Kernel"/> wired to the OpenAI
    /// chat completion connector.
    /// </summary>
    /// <param name="configuration">Application configuration for OpenAI settings.</param>
    public SemanticKernelChatService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI API key is not configured. Use user secrets or environment variables in production.");
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(modelId: model, apiKey: apiKey);
        _kernel = kernelBuilder.Build();
    }

    /// <inheritdoc />
    public async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            return result.ToString();
        }
        catch (ClientResultException ex)
        {
            throw OpenAiExceptionTranslator.Translate(ex);
        }
    }
}
