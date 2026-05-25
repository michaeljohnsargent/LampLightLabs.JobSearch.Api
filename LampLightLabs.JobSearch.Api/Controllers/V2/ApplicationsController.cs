using Asp.Versioning;
using LampLightLabs.JobSearch.Api.Attributes;
using LampLightLabs.JobSearch.Api.Models.V2;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LampLightLabs.JobSearch.Api.Controllers.V2
{
    /// <summary>
    /// V2 - Returns job application data with calculated pipeline intelligence fields.
    /// Demonstrates two authentication schemes:
    /// - GET /fromcsv requires JWT bearer token (user-facing data endpoint).
    /// - GET /status requires an API key (service-to-service status endpoint).
    /// </summary>
    [ApiVersion(2)]
    [Route("api/v{v:apiVersion}/[controller]")]
    [ApiController]
    public class ApplicationsController : ControllerBase
    {
        private readonly ICsvReaderService _csv;
        private readonly IIdempotencyService _idempotency;

        /// <summary>
        /// Constructor that accepts the CSV reader service and idempotency service via dependency injection.
        /// </summary>
        /// <param name="csv">The CSV reader service.</param>
        /// <param name="idempotency">The idempotency service.</param>
        public ApplicationsController(ICsvReaderService csv, IIdempotencyService idempotency)
        {
            _csv = csv;
            _idempotency = idempotency;
        }

        /// <summary>
        /// Returns job applications with calculated fields: DaysInPipeline, IsFollowUpToday, StatusCategory.
        /// Requires JWT bearer token authentication.
        /// </summary>
        /// <returns>A list of enriched job application records.</returns>
        [Authorize]
        [HttpGet("fromcsv")]
        public IActionResult GetFromCsv()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "applications.csv");
            if (!System.IO.File.Exists(filePath))
                return NotFound($"File not found: {filePath}");

            var rows = _csv.ReadCsv(filePath);
            var today = DateOnly.FromDateTime(DateTime.Today);

            var result = rows.Select(row =>
            {
                string Get(string key) => row.ContainsKey(key) ? row[key] : "";

                DateOnly.TryParse(Get("Date applied"), out var dateApplied);
                DateOnly.TryParse(Get("Followup On"), out var followupOn);

                return new ApplicationResponse
                {
                    Company = Get("Company"),
                    Role = Get("Role"),
                    Platform = Get("Platform"),
                    ContactName = Get("Contact Name"),
                    DateApplied = Get("Date applied"),
                    RateBudget = Get("Rate/Budget"),
                    Status = Get("Status"),
                    FollowupOn = Get("Followup On"),
                    Notes = Get("Notes"),
                    LinkToJobPost = Get("Link to job post"),

                    DaysInPipeline = dateApplied != default
                        ? today.DayNumber - dateApplied.DayNumber
                        : 0,
                    IsFollowUpToday = followupOn == today,
                    StatusCategory = CategorizeStatus(Get("Status"))
                };
            }).ToList();

            return Ok(result);
        }

        /// <summary>
        /// Returns a lightweight pipeline status response.
        /// Requires API key authentication via Authorization: ApiKey {key} header.
        /// </summary>
        /// <returns>A status object confirming the API is operational.</returns>
        [ApiKeyAuth]
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                Status = "Operational",
                Version = "v2",
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Returns the count of job applications in the CSV file.
        /// Requires Basic authentication.
        /// </summary>
        /// <returns>The count of job applications.</returns>
        [Authorize(AuthenticationSchemes = "Basic")]
        [HttpGet("count")]
        public IActionResult GetCount ()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "applications.csv");
            if (!System.IO.File.Exists(filePath))
                return NotFound($"File not found: {filePath}");
            var rows = _csv.ReadCsv(filePath);
            return Ok(new { Count = rows.Count() });
        }
        /// <summary>
        /// Creates a new job application record.
        /// Requires JWT bearer token authentication.
        /// Requires an Idempotency-Key header to prevent duplicate submissions.
        ///
        /// - New key: creates the record and caches the response. Returns 201.
        /// - Same key, same body: replays the cached 201. No duplicate is created.
        /// - Same key, different body: returns 422. Key reuse on a different payload is a bug.
        /// - Missing key: returns 400.
        /// </summary>
        [Authorize]
        [HttpPost]
        public IActionResult Post(
            [FromBody] ApplicationRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return BadRequest(new { Error = "Idempotency-Key header is required." });

            // Prefer the user name claim (user JWT); fall back to client_id (client credentials JWT).
            var clientId = User.Identity?.Name
                ?? User.FindFirstValue("client_id")
                ?? "anonymous";

            var requestHash = ComputeHash(request);

            if (_idempotency.TryGetCachedResponse(clientId, idempotencyKey, out var cached))
            {
                if (cached!.RequestHash != requestHash)
                    return UnprocessableEntity(new { Error = "Idempotency-Key reused with a different request body." });

                // Replay the original response — no second record created.
                return StatusCode(cached.StatusCode, cached.Response);
            }

            // First time we have seen this key — build and store the record.
            var today = DateOnly.FromDateTime(DateTime.Today);
            DateOnly.TryParse(request.DateApplied, out var dateApplied);

            var created = new ApplicationResponse
            {
                Company = request.Company,
                Role = request.Role,
                Platform = request.Platform,
                ContactName = request.ContactName,
                DateApplied = request.DateApplied,
                RateBudget = request.RateBudget,
                Status = "Applied",
                Notes = request.Notes,
                LinkToJobPost = request.LinkToJobPost,
                DaysInPipeline = dateApplied != default
                    ? today.DayNumber - dateApplied.DayNumber
                    : 0,
                IsFollowUpToday = false,
                StatusCategory = "Active"
            };

            _idempotency.StoreResponse(clientId, idempotencyKey, requestHash, 201, created);

            return StatusCode(201, created);
        }

        /// <summary>
        /// Computes a SHA-256 hash of the serialized request body.
        /// Used to detect key reuse on a different payload.
        /// </summary>
        private static string ComputeHash(ApplicationRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes);
        }

        private static string CategorizeStatus(string status)
        {
            var s = status.ToLower();

            if (s.Contains("closed") || s.Contains("declined") || s.Contains("rejected"))
                return "Closed";

            if (s.Contains("hold") || s.Contains("waiting") || s.Contains("pending"))
                return "OnHold";

            if (s.Contains("warm") || s.Contains("active") || s.Contains("submitted") || s.Contains("interview"))
                return "Active";

            return "Unknown";
        }

        /// <summary>
        /// Returns aggregate statistics for the job application pipeline.
        /// Requires OAuth 2.0 Client Credentials bearer token (machine-to-machine).
        /// </summary>
        [Authorize]
        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "applications.csv");
            if (!System.IO.File.Exists(filePath))
                return NotFound($"File not found: {filePath}");

            var rows = _csv.ReadCsv(filePath);
            var today = DateOnly.FromDateTime(DateTime.Today);

            var applications = rows.Select(row =>
            {
                string Get(string key) => row.ContainsKey(key) ? row[key] : "";
                DateOnly.TryParse(Get("Date applied"), out var dateApplied);
                DateOnly.TryParse(Get("Followup On"), out var followupOn);
                return new ApplicationResponse
                {
                    Platform = Get("Platform"),
                    DateApplied = Get("Date applied"),
                    Status = Get("Status"),
                    DaysInPipeline = dateApplied != default
                        ? today.DayNumber - dateApplied.DayNumber
                        : 0,
                    IsFollowUpToday = followupOn == today,
                    StatusCategory = CategorizeStatus(Get("Status"))
                };
            }).ToList();

            var validDates = applications
                .Where(a => DateOnly.TryParse(a.DateApplied, out _))
                .Select(a => a.DateApplied)
                .OrderBy(d => d)
                .ToList();

            var stats = new ApplicationStatsResponse
            {
                TotalApplications = applications.Count,
                ByStatusCategory = applications
                    .GroupBy(a => a.StatusCategory)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByPlatform = applications
                    .Where(a => !string.IsNullOrWhiteSpace(a.Platform))
                    .GroupBy(a => a.Platform)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AverageDaysInPipeline = applications.Any()
                    ? Math.Round(applications.Average(a => a.DaysInPipeline), 1)
                    : 0,
                FollowUpsDueToday = applications.Count(a => a.IsFollowUpToday),
                EarliestApplication = validDates.FirstOrDefault() ?? string.Empty,
                MostRecentApplication = validDates.LastOrDefault() ?? string.Empty
            };

            return Ok(stats);
        }
    }
}