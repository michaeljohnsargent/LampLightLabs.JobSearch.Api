namespace LampLightLabs.JobSearch.Api.Models.V1
{
    /// <summary>
    /// Represents a job application as read directly from the pipeline CSV.
    /// </summary>
    public class ApplicationResponse
    {
        public string Company { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string DateApplied { get; set; } = string.Empty;
        public string RateBudget { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FollowupOn { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string LinkToJobPost { get; set; } = string.Empty;
    }
}