namespace LampLightLabs.JobSearch.Api.Services
{
    /// <summary>
    /// Categorizes a raw job application status string into a normalized pipeline category.
    /// </summary>
    public interface IStatusCategorizerService
    {
        /// <summary>
        /// Returns a normalized category for the given raw status value.
        /// Possible return values: "Closed", "OnHold", "Active", "Unknown".
        /// </summary>
        string Categorize(string status);
    }
}
