namespace LampLightLabs.JobSearch.Api.Models.Sk
{
    /// <summary>
    /// Response body for POST /api/sk/chat.
    /// </summary>
    public class SkChatResponse
    {
        /// <summary>
        /// The OpenAI model's text response to the submitted prompt, as returned
        /// through Semantic Kernel.
        /// </summary>
        public string Response { get; set; } = string.Empty;
    }
}
