namespace LampLightLabs.JobSearch.Api.Models
{
    public enum JobStatus { Queued, Processing, Complete, Failed }

    public class JobRecord
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public JobStatus Status { get; set; } = JobStatus.Queued;
        public string? Result { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
