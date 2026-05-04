using LampLightLabs.JobSearch.Api.Models;
using System.Collections.Concurrent;

namespace LampLightLabs.JobSearch.Api.Services
{
    public class JobStore
    {
        private readonly ConcurrentDictionary<string, JobRecord> _jobs = new();

        public JobRecord CreateJob()
        {
            var job = new JobRecord();
            _jobs[job.JobId] = job;
            return job;
        }

        public JobRecord? GetJob(string jobId) =>
            _jobs.TryGetValue(jobId, out var job) ? job : null;

        public void UpdateJob(string jobId, Action<JobRecord> update)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                update(job);
        }
    }
}
