namespace LampLightLabs.JobSearch.Api.Services;

public interface IPromptRepository
{
    string GetRagMatchSystemPrompt();
    string BuildRagMatchUserMessage(string jobDescription, IEnumerable<string> resumeChunks);
}
