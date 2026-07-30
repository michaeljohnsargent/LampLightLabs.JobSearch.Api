using LampLightLabs.JobSearch.Api.Models.Ai;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LampLightLabs.JobSearch.Api.Controllers.V2
{
    /// <summary>
    /// Exposes an endpoint for sending prompts to the Anthropic Claude API.
    /// </summary>
    [Route("api/v2/ai")]
    [ApiController]
    [EnableRateLimiting("ai-token-bucket")]
    public class AiController : ControllerBase
    {
        private readonly IClaudeChatService _claudeChatService;
        private readonly ILogger<AiController> _logger;

        /// <summary>
        /// Required constructor that accepts dependencies via dependency injection.
        /// </summary>
        /// <param name="claudeChatService">The Claude chat service.</param>
        /// <param name="logger">Logger used to record the full detail of upstream failures server-side.</param>
        public AiController(IClaudeChatService claudeChatService, ILogger<AiController> logger)
        {
            _claudeChatService = claudeChatService;
            _logger = logger;
        }

        /// <summary>
        /// Sends the supplied prompt to Claude and returns its response.
        /// </summary>
        /// <param name="request">The request body containing the prompt.</param>
        /// <param name="cancellationToken">Token used to cancel the request.</param>
        /// <returns>Claude's response to the prompt.</returns>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest(new { Error = "Prompt is required." });

            try
            {
                var responseText = await _claudeChatService.SendPromptAsync(request.Prompt, cancellationToken);
                return Ok(new AiChatResponse { Response = responseText });
            }
            catch (AiProviderException ex) when (ex.Reason == AiProviderFailureReason.RateLimited)
            {
                _logger.LogError(ex, "AI chat failed: {Provider} rate limit exceeded", ex.Provider);
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { Error = "The AI service is rate limited. Please try again shortly." });
            }
            catch (AiProviderException ex)
            {
                // Never surface the SDK's raw message here — it can carry account/billing detail.
                _logger.LogError(ex, "AI chat failed: {Provider} unavailable ({Reason})", ex.Provider, ex.Reason);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { Error = "This service is temporarily unavailable. Please try again later." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI chat failed unexpectedly.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { Error = "This service is temporarily unavailable. Please try again later." });
            }
        }
    }
}
