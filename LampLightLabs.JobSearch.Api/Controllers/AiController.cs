using LampLightLabs.JobSearch.Api.Models.Ai;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    /// <summary>
    /// Exposes an endpoint for sending prompts to the Anthropic Claude API.
    /// </summary>
    [Route("api/ai")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly IClaudeChatService _claudeChatService;

        /// <summary>
        /// Required constructor that accepts dependencies via dependency injection.
        /// </summary>
        /// <param name="claudeChatService">The Claude chat service.</param>
        public AiController(IClaudeChatService claudeChatService)
        {
            _claudeChatService = claudeChatService;
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

            var responseText = await _claudeChatService.SendPromptAsync(request.Prompt, cancellationToken);

            return Ok(new AiChatResponse { Response = responseText });
        }
    }
}
