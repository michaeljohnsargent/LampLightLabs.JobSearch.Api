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
    private readonly IUsageTrackingService _usageTrackingService;
    private readonly ILogger<RagController> _logger;

    public RagController(
        IRagMatchService ragMatchService,
        IUsageTrackingService usageTrackingService,
        ILogger<RagController> logger)
    {
        _ragMatchService = ragMatchService;
        _usageTrackingService = usageTrackingService;
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

        // Demo/production toggle and cost circuit breaker share one decision point — both mean
        // "don't run the real pipeline" and degrade to the exact same response shape the client
        // already knows how to handle (shows the canned demo buttons).
        if (await _usageTrackingService.ShouldServeDemoAsync(cancellationToken))
            return DemoUnavailableResult(costLimited: true);

        try
        {
            var result = await _ragMatchService.MatchAsync(sanitized, cancellationToken);

            try
            {
                await _usageTrackingService.LogUsageAsync("api/rag/match", cancellationToken);
            }
            catch (Exception ex)
            {
                // Never fail a successful match response because usage tracking couldn't write —
                // the user already got their result, this is purely internal bookkeeping.
                _logger.LogWarning(ex, "Failed to log usage for job description hash {Hash}", HashForLogging(sanitized));
            }

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
            return DemoUnavailableResult(costLimited: false);
        }
        catch (Exception ex)
        {
            // Last-resort safety net: never let a raw exception message or stack trace reach the
            // client, even for failures unrelated to the AI providers above (e.g. malformed LLM
            // output that fails to parse).
            _logger.LogError(ex, "RAG match failed unexpectedly for job description hash {Hash}", HashForLogging(sanitized));
            return DemoUnavailableResult(costLimited: false);
        }
    }

    [HttpGet("usage")]
    [DisableRateLimiting]
    public async Task<IActionResult> Usage(CancellationToken cancellationToken)
    {
        var summary = await _usageTrackingService.GetCurrentMonthSummaryAsync(cancellationToken);
        return Ok(new UsageSummaryResponse
        {
            TotalCostUsd = summary.TotalCostUsd,
            PercentOfBudgetUsed = summary.PercentOfBudgetUsed,
            HasHitHardCeiling = summary.HasHitHardCeiling
        });
    }

    // costLimited distinguishes the deliberate demo-toggle/circuit-breaker path (ShouldServeDemoAsync
    // returned true, nothing actually failed) from a genuine outage/unexpected exception — the two
    // situations need different, honest copy, but share the same response shape (Error + TryDemo) so
    // the client's existing scroll-to-demo-section handling needs no changes either way.
    private ObjectResult DemoUnavailableResult(bool costLimited) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            Error = costLimited
                ? "Live analysis is limited to manage API costs. Try a sample result above to see how it works."
                : "Something went wrong on our end. Try a sample result above while we look into it.",
            TryDemo = true
        });

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
