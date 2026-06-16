namespace LampLightLabs.JobSearch.Api.Models.Ai
{
    /// <summary>
    /// Request body for POST /api/ai/chat.
    /// </summary>
    public class AiChatRequest
    {
        /// <summary>
        /// The prompt to send to Claude.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;
    }
}
