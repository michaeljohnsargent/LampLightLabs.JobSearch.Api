using Asp.Versioning;
using LampLightLabs.JobSearch.Api.Attributes;
using LampLightLabs.JobSearch.Api.Models.V2;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        /// <summary>
        /// Constructor that accepts the CSV reader service via dependency injection.
        /// </summary>
        /// <param name="csv">The CSV reader service.</param>
        public ApplicationsController(ICsvReaderService csv)
        {
            _csv = csv;
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
    }
}