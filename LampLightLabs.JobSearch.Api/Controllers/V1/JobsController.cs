using Asp.Versioning;
using LampLightLabs.JobSearch.Api.Models;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    /// <summary>
    /// Controller for managing job processing.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{v:apiVersion}/[controller]")]
    [ApiController]
    [EnableRateLimiting("api-sliding")]
    public class JobsController : ControllerBase
    {
        private readonly IJobStore _jobStore;
        private readonly ICsvReaderService _csv;
        private readonly IWebHostEnvironment _env;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JobsController> _logger;

        /// <summary>
        /// Required constructor that accepts dependencies via dependency injection.
        /// </summary>
        /// <param name="jobStore">The job store for tracking job records — EF Core/Postgres-backed
        /// in production (see <see cref="EfJobStore"/>), swappable for the in-memory
        /// <see cref="JobStore"/> via the <c>Program.cs</c> DI registration.</param>
        /// <param name="csv">The CSV reader service for reading CSV files.</param>
        /// <param name="env">The web hosting environment.</param>
        /// <param name="scopeFactory">Used to create a fresh DI scope for the background
        /// job (see <see cref="ProcessApplicationsAsync"/>) rather than reusing this
        /// controller's request-scoped services after the request has returned.</param>
        /// <param name="logger">Logger for this controller. No test constructs this
        /// controller directly (see <c>Program.cs</c>/integration-style testing), so this
        /// stays a required parameter rather than the optional-with-NullLogger pattern
        /// used in <see cref="JobStore"/>/<see cref="EfJobStore"/>.</param>
        public JobsController(
            IJobStore jobStore,
            ICsvReaderService csv,
            IWebHostEnvironment env,
            IServiceScopeFactory scopeFactory,
            ILogger<JobsController> logger)
        {
            _jobStore = jobStore;
            _csv = csv;
            _env = env;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // POST api/jobs/start
        /// <summary>
        /// Starts a new job and initiates background processing of applications.
        /// Creates a CancellationTokenSource and passes the token into the background
        /// task so processing can be cancelled cleanly if needed.
        /// </summary>
        /// <returns>An Accepted response containing the job ID and status.</returns>
        [HttpPost("start")]
        public async Task<IActionResult> StartJob()
        {
            // Runs and completes before the response is returned, so it's safe to use
            // this controller's own request-scoped _jobStore here.
            var job = await _jobStore.CreateJobAsync();
            _logger.LogInformation("Job {JobId} created, starting background processing", job.JobId);

            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            // Kick off background work without awaiting it
            var _ = Task.Run(() => ProcessApplicationsAsync(job.JobId, token));

            return Accepted(new { jobId = job.JobId, status = job.Status.ToString() });
        }


        // GET api/jobs/{jobId}/status
        /// <summary>
        /// Retrieves the status of a specific job.
        /// </summary>
        /// <param name="jobId">The ID of the job to retrieve the status for.</param>
        /// <returns>The status of the specified job.</returns>
        [HttpGet("{jobId}/status")]
        public async Task<IActionResult> GetStatus(string jobId)
        {
            var job = await _jobStore.GetJobAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Status check for unknown job {JobId}", jobId);
                return NotFound($"Job {jobId} not found.");
            }

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
        /// The delay is cancellation-aware, and cancellation is checked again before CSV
        /// processing starts so the operation exits cleanly at a safe boundary.
        ///
        /// Runs detached from the HTTP request that started it (fired via
        /// <c>Task.Run</c>, not awaited). By the time this executes, ASP.NET Core may
        /// already have disposed the request's DI scope — and with it, this
        /// controller's own <c>_jobStore</c>/<c>_csv</c> fields, since both are
        /// registered Scoped and the EF Core-backed store holds a DbContext that isn't
        /// safe to touch after its scope is disposed. Using those fields directly here
        /// would risk an <see cref="ObjectDisposedException"/> on the DbContext, or
        /// worse, silent corruption if the request scope happens to still be alive.
        /// Instead, a fresh scope is created explicitly so this background work gets
        /// its own Scoped instances with a lifetime tied to the work itself rather than
        /// to the request that kicked it off — the standard fix for fire-and-forget
        /// work in ASP.NET Core (see Microsoft's docs on avoiding captive dependencies).
        /// </summary>
        /// <param name="jobId">The ID of the job to process applications for.</param>
        /// <param name="cancellationToken">Token used to cancel the operation cleanly
        /// before or during processing.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessApplicationsAsync(string jobId, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var jobStore = scope.ServiceProvider.GetRequiredService<IJobStore>();
            var csv = scope.ServiceProvider.GetRequiredService<ICsvReaderService>();

            _logger.LogDebug("Job {JobId} background processing started in a fresh DI scope", jobId);

            await jobStore.UpdateJobAsync(jobId, j => j.Status = JobStatus.Processing);

            try
            {
                // Simulate real work - read and process the CSV
                await Task.Delay(3000, cancellationToken); // 3 second delay to simulate long-running work

                cancellationToken.ThrowIfCancellationRequested();

                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "applications.csv");
                var rows = csv.ReadCsv(filePath).ToList();

                await jobStore.UpdateJobAsync(jobId, j =>
                {
                    j.Status = JobStatus.Complete;
                    j.Result = $"Processed {rows.Count} applications successfully.";
                    j.CompletedAt = DateTime.UtcNow;
                });

                _logger.LogInformation("Job {JobId} completed successfully, processed {RowCount} applications", jobId, rows.Count);
            }
            catch (Exception ex)
            {
                // Cancellation and genuine failures still update the job identically
                // (existing behavior, unchanged) — only the log level/detail differs,
                // since a deliberate cancellation isn't really an "error."
                if (ex is OperationCanceledException)
                    _logger.LogWarning("Job {JobId} was cancelled before completion", jobId);
                else
                    _logger.LogError(ex, "Job {JobId} failed during processing", jobId);

                await jobStore.UpdateJobAsync(jobId, j =>
                {
                    j.Status = JobStatus.Failed;
                    j.Result = $"Error: {ex.Message}";
                    j.CompletedAt = DateTime.UtcNow;
                });
            }
        }
    }
}
