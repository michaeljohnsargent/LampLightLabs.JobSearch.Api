using LampLightLabs.JobSearch.Api.Models.Sk;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LampLightLabs.JobSearch.Api.Controllers.V2
{
    /// <summary>
    /// Exposes an endpoint for sending prompts to an OpenAI model through
    /// Microsoft Semantic Kernel.
    /// </summary>
    [Route("api/v2/sk")]
    [ApiController]
    [EnableRateLimiting("ai-token-bucket")]
    public class SemanticKernelController : ControllerBase
    {
        private readonly ISemanticKernelChatService _semanticKernelChatService;
        private readonly ILogger<SemanticKernelController> _logger;

        /// <summary>
        /// Required constructor that accepts dependencies via dependency injection.
        /// </summary>
        /// <param name="semanticKernelChatService">The Semantic Kernel chat service.</param>
        /// <param name="logger">Logger used to record the full detail of upstream failures server-side.</param>
        public SemanticKernelController(ISemanticKernelChatService semanticKernelChatService, ILogger<SemanticKernelController> logger)
        {
            _semanticKernelChatService = semanticKernelChatService;
            _logger = logger;
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

            try
            {
                var responseText = await _semanticKernelChatService.SendPromptAsync(request.Prompt, cancellationToken);
                return Ok(new SkChatResponse { Response = responseText });
            }
            catch (AiProviderException ex) when (ex.Reason == AiProviderFailureReason.RateLimited)
            {
                _logger.LogError(ex, "SK chat failed: {Provider} rate limit exceeded", ex.Provider);
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { Error = "The AI service is rate limited. Please try again shortly." });
            }
            catch (AiProviderException ex)
            {
                // Never surface the SDK's raw message here — it can carry account/billing detail.
                _logger.LogError(ex, "SK chat failed: {Provider} unavailable ({Reason})", ex.Provider, ex.Reason);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { Error = "This service is temporarily unavailable. Please try again later." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SK chat failed unexpectedly.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { Error = "This service is temporarily unavailable. Please try again later." });
            }
        }
    }
}
