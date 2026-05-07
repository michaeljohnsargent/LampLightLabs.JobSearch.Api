using Asp.Versioning;
using LampLightLabs.JobSearch.Api.Models;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    /// <summary>
    /// Controller for managing job processing.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{v:apiVersion}/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly JobStore _jobStore;
        private readonly ICsvReaderService _csv;
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Required constructor that accepts dependencies via dependency injection.
        /// </summary>
        /// <param name="jobStore">The in-memory job store for tracking job records.</param>
        /// <param name="csv">The CSV reader service for reading CSV files.</param>
        /// <param name="env">The web hosting environment.</param>
        public JobsController(JobStore jobStore, ICsvReaderService csv, IWebHostEnvironment env)
        {
            _jobStore = jobStore;
            _csv = csv;
            _env = env;
        }

        // POST api/jobs/start
        /// <summary>
        /// Starts a new job and initiates background processing of applications.
        /// </summary>
        /// <returns>An Accepted response containing the job ID and status.</returns>
        [HttpPost("start")]
        public IActionResult StartJob()
        {
            var job = _jobStore.CreateJob();

            // Kick off background work without awaiting it
            _ = Task.Run(() => ProcessApplicationsAsync(job.JobId));

            return Accepted(new { jobId = job.JobId, status = job.Status.ToString() });
        }


        // GET api/jobs/{jobId}/status
        /// <summary>
        /// Retrieves the status of a specific job.
        /// </summary>
        /// <param name="jobId">The ID of the job to retrieve the status for.</param>
        /// <returns>The status of the specified job.</returns>
        [HttpGet("{jobId}/status")]
        public IActionResult GetStatus(string jobId)
        {
            var job = _jobStore.GetJob(jobId);
            if (job == null)
                return NotFound($"Job {jobId} not found.");

            return Ok(new
            {
                jobId = job.JobId,
                status = job.Status.ToString(),
                result = job.Result,
                createdAt = job.CreatedAt,
                completedAt = job.CompletedAt
            });
        }

        /// <summary>
        /// Reads and processes applications from a CSV file, updating the job status 
        /// accordingly. This method simulates long-running work by introducing a delay 
        /// and then reading a CSV file to count the number of applications processed. 
        /// It updates the job record in the in-memory store with the results or any 
        /// errors encountered during processing.
        /// </summary>
        /// <param name="jobId">The ID of the job to process applications for.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessApplicationsAsync(string jobId)
        {
            _jobStore.UpdateJob(jobId, j => j.Status = JobStatus.Processing);

            try
            {
                // Simulate real work - read and process the CSV
                await Task.Delay(3000); // 3 second delay to simulate long-running work

                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "applications.csv");
                var rows = _csv.ReadCsv(filePath).ToList();

                _jobStore.UpdateJob(jobId, j =>
                {
                    j.Status = JobStatus.Complete;
                    j.Result = $"Processed {rows.Count} applications successfully.";
                    j.CompletedAt = DateTime.UtcNow;
                });
            }
            catch (Exception ex)
            {
                _jobStore.UpdateJob(jobId, j =>
                {
                    j.Status = JobStatus.Failed;
                    j.Result = $"Error: {ex.Message}";
                    j.CompletedAt = DateTime.UtcNow;
                });
            }
        }
    }
}