using LampLightLabs.JobSearch.Api.Models;
using System.Collections.Concurrent;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// In-memory store for tracking background job records.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// </summary>
    public class JobStore
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
    }
}
