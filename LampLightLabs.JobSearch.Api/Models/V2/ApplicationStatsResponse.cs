namespace LampLightLabs.JobSearch.Api.Models.V2
{
    /// <summary>
    /// Represents aggregate statistics for the job application pipeline.
    /// </summary>
    public class ApplicationStatsResponse
    {
        public int TotalApplications { get; set; }
        public Dictionary<string, int> ByStatusCategory { get; set; } = new();
        public Dictionary<string, int> ByPlatform { get; set; } = new();
        public double AverageDaysInPipeline { get; set; }
        public int FollowUpsDueToday { get; set; }
        public string EarliestApplication { get; set; } = string.Empty;
        public string MostRecentApplication { get; set; } = string.Empty;
    }
}