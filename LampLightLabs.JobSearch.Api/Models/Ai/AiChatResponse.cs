namespace LampLightLabs.JobSearch.Api.Models.Ai
{
    /// <summary>
    /// Response body for POST /api/ai/chat.
    /// </summary>
    public class AiChatResponse
    {
        /// <summary>
        /// Claude's text response to the submitted prompt.
        /// </summary>
        public string Response { get; set; } = string.Empty;
    }
}
