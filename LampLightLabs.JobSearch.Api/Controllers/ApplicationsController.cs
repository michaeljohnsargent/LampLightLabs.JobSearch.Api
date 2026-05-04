using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    /// <summary>
    /// Controller for retrieving job application pipeline data.
    /// </summary>
    [Route("api/[controller]")]
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
        /// Reads job applications from the pipeline CSV file and returns them as a list.
        /// </summary>
        /// <returns>A list of job applications with their current pipeline states.</returns>
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