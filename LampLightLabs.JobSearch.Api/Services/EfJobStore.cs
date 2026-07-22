using LampLightLabs.JobSearch.Api.Data;
using LampLightLabs.JobSearch.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// EF Core-backed job store (Postgres in production via <see cref="JobSearchDbContext"/>).
    ///
    /// Must be registered Scoped, matching the DbContext it depends on. A Singleton
    /// service holding a Scoped DbContext is the classic "captive dependency" bug: the
    /// container would resolve the DbContext once, on first use, and this service would
    /// then hold that single instance for the rest of the application's lifetime —
    /// silently defeating the whole point of Scoped and reusing one non-thread-safe
    /// DbContext across every concurrent request. Registering this class Scoped means a
    /// fresh instance (and a fresh DbContext) is created per request, which is what
    /// actually matches the lifetime of "one unit of work."
    /// </summary>
    public class EfJobStore : IJobStore
    {
        private readonly JobSearchDbContext _db;
        private readonly ILogger<EfJobStore> _logger;

        /// <summary>
        /// Logger defaults to <see cref="NullLogger{T}"/> when not supplied so the
        /// existing <c>new EfJobStore(db)</c> call sites in <c>EfJobStoreTests.cs</c>
        /// keep compiling unchanged. Production DI always resolves a real
        /// <see cref="ILogger{EfJobStore}"/> here since the parameter type is registered
        /// in the container regardless of the default value.
        /// </summary>
        public EfJobStore(JobSearchDbContext db, ILogger<EfJobStore>? logger = null)
        {
            _db = db;
            _logger = logger ?? NullLogger<EfJobStore>.Instance;
        }

        public async Task<JobRecord> CreateJobAsync()
        {
            var job = new JobRecord();
            _db.Jobs.Add(job);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Job {JobId} created in Postgres store", job.JobId);
            return job;
        }

        public async Task<JobRecord?> GetJobAsync(string jobId)
        {
            var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job == null)
                _logger.LogWarning("GetJobAsync: job {JobId} not found in Postgres store", jobId);

            return job;
        }

        public async Task UpdateJobAsync(string jobId, Action<JobRecord> update)
        {
            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job == null)
            {
                _logger.LogWarning("UpdateJobAsync: job {JobId} not found in Postgres store, update skipped", jobId);
                return;
            }

            update(job);
            await _db.SaveChangesAsync();
            _logger.LogDebug("Job {JobId} updated in Postgres store, status now {Status}", jobId, job.Status);
        }
    }
}
