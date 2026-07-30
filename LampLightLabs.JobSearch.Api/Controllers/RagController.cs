using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LampLightLabs.JobSearch.Api.Models.Rag;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LampLightLabs.JobSearch.Api.Controllers;

[Route("api/rag")]
[ApiController]
[EnableRateLimiting("ai-token-bucket")]
public class RagController : ControllerBase
{
    private readonly IRagMatchService _ragMatchService;
    private readonly ILogger<RagController> _logger;

    public RagController(IRagMatchService ragMatchService, ILogger<RagController> logger)
    {
        _ragMatchService = ragMatchService;
        _logger = logger;
    }

    [HttpPost("match")]
    public async Task<IActionResult> Match([FromBody] RagMatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JobDescription))
            return BadRequest(new { Error = "JobDescription is required." });

        var sanitized = SanitizeJobDescription(request.JobDescription);

        if (string.IsNullOrWhiteSpace(sanitized))
            return BadRequest(new { Error = "JobDescription is required." });

        try
        {
            var result = await _ragMatchService.MatchAsync(sanitized, cancellationToken);
            return Ok(result);
        }
        catch (AiProviderException ex) when (ex.Reason == AiProviderFailureReason.RateLimited)
        {
            _logger.LogError(ex, "RAG match failed for job description hash {Hash}: {Provider} rate limit exceeded",
                HashForLogging(sanitized), ex.Provider);
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { Error = "The AI service is rate limited. Please try again shortly." });
        }
        catch (AiProviderException ex)
        {
            // Covers Billing, Unauthorized, Unavailable, and Unknown alike — none of these are
            // the caller's fault, and the billing/auth detail isn't something a client can act on.
            // The demo buttons give a visitor a working alternative instead of a dead end.
            _logger.LogError(ex, "RAG match failed for job description hash {Hash}: {Provider} unavailable ({Reason})",
                HashForLogging(sanitized), ex.Provider, ex.Reason);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Error = "This demo is temporarily unavailable. Try a sample result below while it's back.",
                TryDemo = true
            });
        }
        catch (Exception ex)
        {
            // Last-resort safety net: never let a raw exception message or stack trace reach the
            // client, even for failures unrelated to the AI providers above (e.g. malformed LLM
            // output that fails to parse).
            _logger.LogError(ex, "RAG match failed unexpectedly for job description hash {Hash}", HashForLogging(sanitized));
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Error = "This demo is temporarily unavailable. Try a sample result below while it's back.",
                TryDemo = true
            });
        }
    }

    // Short, non-reversible correlation id for logs — avoids logging the job description text itself.
    private static string HashForLogging(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..12];

    // Strips non-printable control characters, normalizes line endings, and
    // collapses runs of horizontal whitespace so the LLM receives clean input.
    private static string SanitizeJobDescription(string input)
    {
        // Remove non-printable control characters, keeping \t (\x09), \n (\x0A), \r (\x0D)
        var result = Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty);
        // Normalize line endings to \n
        result = result.Replace("\r\n", "\n").Replace("\r", "\n");
        // Collapse horizontal whitespace (spaces, tabs) to a single space per run
        result = Regex.Replace(result, @"[^\S\n]+", " ");
        // Cap consecutive blank lines at one (3+ newlines → 2)
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        return result.Trim();
    }
}
