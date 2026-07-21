using LampLightLabs.JobSearch.Api.Data;
using LampLightLabs.JobSearch.Api.Models;
using Microsoft.EntityFrameworkCore;

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

        public EfJobStore(JobSearchDbContext db)
        {
            _db = db;
        }

        public async Task<JobRecord> CreateJobAsync()
        {
            var job = new JobRecord();
            _db.Jobs.Add(job);
            await _db.SaveChangesAsync();
            return job;
        }

        public async Task<JobRecord?> GetJobAsync(string jobId) =>
            await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == jobId);

        public async Task UpdateJobAsync(string jobId, Action<JobRecord> update)
        {
            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            if (job == null)
                return;

            update(job);
            await _db.SaveChangesAsync();
        }
    }
}
