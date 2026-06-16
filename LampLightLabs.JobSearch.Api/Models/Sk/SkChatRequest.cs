namespace LampLightLabs.JobSearch.Api.Models.Sk
{
    /// <summary>
    /// Request body for POST /api/sk/chat.
    /// </summary>
    public class SkChatRequest
    {
        /// <summary>
        /// The prompt to send to the OpenAI model via Semantic Kernel.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;
    }
}
