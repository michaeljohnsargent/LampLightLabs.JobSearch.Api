using Asp.Versioning;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LampLightLabs.JobSearch.Api.Controllers.V1
{
    /// <summary>
    /// V1 - Returns raw job application data from the pipeline CSV.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{v:apiVersion}/[controller]")]
    [ApiController]
    [EnableRateLimiting("api-sliding")]
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
        /// Returns raw job applications read directly from the pipeline CSV.
        /// </summary>
        /// <returns>A list of raw job application records.</returns>
        [HttpGet("fromcsv")]
        public IActionResult GetFromCsv()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "applications.csv");
            if (!System.IO.File.Exists(filePath))
                return NotFound($"File not found: {filePath}");

            var rows = _csv.ReadCsv(filePath);
            return Ok(rows);
        }
    }
}