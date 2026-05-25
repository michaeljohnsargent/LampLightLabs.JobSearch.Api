namespace LampLightLabs.JobSearch.Api.Models.V2
{
    /// <summary>
    /// Represents a new job application submission.
    /// Sent as the request body to POST /api/v2/applications.
    /// </summary>
    public class ApplicationRequest
    {
        public string Company { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string DateApplied { get; set; } = string.Empty;
        public string RateBudget { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string LinkToJobPost { get; set; } = string.Empty;
    }
}
