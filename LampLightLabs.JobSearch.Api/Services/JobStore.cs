using LampLightLabs.JobSearch.Api.Models;
using System.Collections.Concurrent;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// In-memory store for tracking background job records.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// Registered Singleton in <c>Program.cs</c> — safe here specifically because a
    /// <c>ConcurrentDictionary</c> is designed for concurrent access from a single
    /// shared instance, unlike <see cref="Data.JobSearchDbContext"/>, which is not
    /// thread-safe and must be Scoped instead (see <see cref="EfJobStore"/>).
    /// Implements <see cref="IJobStore"/> explicitly so this class keeps its original
    /// synchronous public API (existing tests call it directly) while still satisfying
    /// the async interface that <c>JobsController</c> depends on.
    /// </summary>
    public class JobStore : IJobStore
    {
        private readonly ConcurrentDictionary<string, JobRecord> _jobs = new();

        /// <summary>
        /// Creates a new job record with default Queued status and adds it to the store.
        /// </summary>
        /// <returns>The newly created <see cref="JobRecord"/>.</returns>
        public JobRecord CreateJob()
        {
            var job = new JobRecord();
            _jobs[job.JobId] = job;
            return job;
        }

        /// <summary>
        /// Retrieves a job record by its ID. Returns null if the job does not exist.
        /// </summary>
        /// <param name="jobId">The ID of the job to retrieve.</param>
        /// <returns>The <see cref="JobRecord"/> if found; otherwise, null.</returns>
        public JobRecord? GetJob(string jobId) =>
            _jobs.TryGetValue(jobId, out var job) ? job : null;

        /// <summary>
        /// Updates a job record by its ID. Does nothing if the job does not exist.
        /// </summary>
        /// <param name="jobId">The ID of the job to update.</param>
        /// <param name="update">The action to perform on the job record.</param>
        public void UpdateJob(string jobId, Action<JobRecord> update)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                update(job);
        }

        // IJobStore explicit implementation — thin async wrappers around the sync
        // methods above. Explicit (rather than public async methods with different
        // names) so the class's original sync surface — the one JobStoreTests.cs
        // exercises directly — is completely unchanged.
        Task<JobRecord> IJobStore.CreateJobAsync() => Task.FromResult(CreateJob());

        Task<JobRecord?> IJobStore.GetJobAsync(string jobId) => Task.FromResult(GetJob(jobId));

        Task IJobStore.UpdateJobAsync(string jobId, Action<JobRecord> update)
        {
            UpdateJob(jobId, update);
            return Task.CompletedTask;
        }
    }
}
