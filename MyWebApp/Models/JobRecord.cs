namespace LampLightLabs.JobSearch.Api.Models
{
    /// <summary>
    /// Represents the possible states of a background processing job.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>Job has been created and is waiting to be picked up.</summary>
        Queued,
        /// <summary>Job is actively being processed.</summary>
        Processing,
        /// <summary>Job completed successfully.</summary>
        Complete,
        /// <summary>Job encountered an error during processing.</summary>
        Failed
    }

    /// <summary>
    /// Represents a background job record tracked in the in-memory job store.
    /// </summary>
    public class JobRecord
    {
        /// <summary>
        /// Unique identifier for this job. Generated automatically on creation.
        /// </summary>
        public string JobId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Current status of the job. Defaults to <see cref="JobStatus.Queued"/>.
        /// </summary>
        public JobStatus Status { get; set; } = JobStatus.Queued;

        /// <summary>
        /// Result message populated when the job completes or fails.
        /// Null while the job is Queued or Processing.
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// UTC timestamp when the job was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the job finished. Null until the job reaches
        /// Complete or Failed status.
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }
}