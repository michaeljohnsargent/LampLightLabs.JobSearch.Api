using LampLightLabs.JobSearch.Api.Models.Sk;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    /// <summary>
    /// Exposes an endpoint for sending prompts to an OpenAI model through
    /// Microsoft Semantic Kernel.
    /// </summary>
    [Route("api/sk")]
    [ApiController]
    public class SemanticKernelController : ControllerBase
    {
        private readonly ISemanticKernelChatService _semanticKernelChatService;

        /// <summary>
        /// Required constructor that accepts dependencies via dependency injection.
        /// </summary>
        /// <param name="semanticKernelChatService">The Semantic Kernel chat service.</param>
        public SemanticKernelController(ISemanticKernelChatService semanticKernelChatService)
        {
            _semanticKernelChatService = semanticKernelChatService;
        }

        /// <summary>
        /// Sends the supplied prompt to the configured OpenAI model via
        /// Semantic Kernel and returns its response.
        /// </summary>
        /// <param name="request">The request body containing the prompt.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>The model's response to the prompt.</returns>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] SkChatRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest(new { Error = "Prompt is required." });

            var responseText = await _semanticKernelChatService.SendPromptAsync(request.Prompt, cancellationToken);

            return Ok(new SkChatResponse { Response = responseText });
        }
    }
}
