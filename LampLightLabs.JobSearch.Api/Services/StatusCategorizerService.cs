namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Categorizes raw job application status strings into normalized pipeline categories.
    ///
    /// This logic was extracted from a private static method in ApplicationsController
    /// to make it independently testable. Characterization tests in
    /// StatusCategorizerCharacterizationTests freeze the current behavior before
    /// any refactoring is attempted.
    /// </summary>
    public class StatusCategorizerService : IStatusCategorizerService
    {
        /// <inheritdoc />
        public string Categorize(string status)
        {
            var s = status.ToLower();

            if (s.Contains("closed") || s.Contains("declined") || s.Contains("rejected"))
                return "Closed";

            if (s.Contains("hold") || s.Contains("waiting") || s.Contains("pending"))
                return "OnHold";

            if (s.Contains("warm") || s.Contains("active") || s.Contains("submitted") || s.Contains("interview"))
                return "Active";

            return "Unknown";
        }
    }
}
