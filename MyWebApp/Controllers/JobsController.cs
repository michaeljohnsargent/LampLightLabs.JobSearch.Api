using LampLightLabs.JobSearch.Api.Models;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly JobStore _jobStore;
        private readonly ICsvReaderService _csv;
        private readonly IWebHostEnvironment _env;

        public JobsController(JobStore jobStore, ICsvReaderService csv, IWebHostEnvironment env)
        {
            _jobStore = jobStore;
            _csv = csv;
            _env = env;
        }

        // POST api/jobs/start
        [HttpPost("start")]
        public IActionResult StartJob()
        {
            var job = _jobStore.CreateJob();

            // Kick off background work without awaiting it
            _ = Task.Run(() => ProcessApplicationsAsync(job.JobId));

            return Accepted(new { jobId = job.JobId, status = job.Status.ToString() });
        }

        // GET api/jobs/{jobId}/status
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