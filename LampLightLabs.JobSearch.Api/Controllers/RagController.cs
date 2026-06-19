using LampLightLabs.JobSearch.Api.Models.Rag;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LampLightLabs.JobSearch.Api.Controllers;

[Route("api/rag")]
[ApiController]
public class RagController : ControllerBase
{
    private readonly IRagMatchService _ragMatchService;

    public RagController(IRagMatchService ragMatchService)
    {
        _ragMatchService = ragMatchService;
    }

    [HttpPost("match")]
    public async Task<IActionResult> Match([FromBody] RagMatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JobDescription))
            return BadRequest(new { Error = "JobDescription is required." });

        var result = await _ragMatchService.MatchAsync(request.JobDescription, cancellationToken);
        return Ok(result);
    }
}
