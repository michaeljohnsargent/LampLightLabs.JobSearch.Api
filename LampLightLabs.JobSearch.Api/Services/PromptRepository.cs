namespace LampLightLabs.JobSearch.Api.Services;

public class PromptRepository : IPromptRepository
{
    private const string SystemPrompt =
        "You are a professional career advisor. Analyze how well a candidate's resume matches the given job description. " +
        "Return only a raw JSON object with these exact keys: matchScore (integer 0-100 representing overall fit), " +
        "summary (2-3 sentence narrative of fit), strengths (array of strings describing specific things the candidate brings), " +
        "gaps (array of strings describing qualifications or skills the candidate is missing). " +
        "No markdown fences. No explanation. Nothing before or after the JSON.";

    public string GetRagMatchSystemPrompt() => SystemPrompt;

    public string BuildRagMatchUserMessage(string jobDescription, IEnumerable<string> resumeChunks)
    {
        var context = string.Join("\n\n", resumeChunks);
        return $"""
            Job Description:
            {jobDescription}

            Resume Context:
            {context}
            """;
    }
}
