namespace LampLightLabs.JobSearch.Api.Models.Rag;

public class RagMatchResponse
{
    public int MatchScore { get; init; }
    public string Summary { get; init; } = "";
    public List<string> Strengths { get; init; } = [];
    public List<string> Gaps { get; init; } = [];
    public List<string> RetrievedContext { get; init; } = [];
}
