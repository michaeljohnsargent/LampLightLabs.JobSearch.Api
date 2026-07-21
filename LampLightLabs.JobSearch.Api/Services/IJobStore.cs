using LampLightLabs.JobSearch.Api.Models;

namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Contract for job-record persistence. Same Strategy Pattern already used for
    /// <see cref="ICsvReaderService"/>: <see cref="JobStore"/> (in-memory,
    /// <c>ConcurrentDictionary</c>-backed) and <see cref="EfJobStore"/> (EF Core /
    /// Postgres-backed) both implement this interface and are interchangeable via DI —
    /// swapping which one is registered in <c>Program.cs</c> is a one-line change, and
    /// callers (<c>JobsController</c>) depend only on the interface.
    ///
    /// Methods are async because the EF Core implementation always is; the in-memory
    /// implementation satisfies this contract with trivial <c>Task</c>-wrapped calls.
    /// </summary>
    public interface IJobStore
    {
        /// <summary>Creates a new job record with default Queued status and persists it.</summary>
        Task<JobRecord> CreateJobAsync();

        /// <summary>Retrieves a job record by its ID. Returns null if the job does not exist.</summary>
        Task<JobRecord?> GetJobAsync(string jobId);

        /// <summary>Applies an update to an existing job record. Does nothing if the job does not exist.</summary>
        Task UpdateJobAsync(string jobId, Action<JobRecord> update);
    }
}
