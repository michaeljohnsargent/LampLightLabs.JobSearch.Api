using System.Text.Json;
using System.Text.Json.Serialization;
using LampLightLabs.JobSearch.Api.Models.Rag;

namespace LampLightLabs.JobSearch.Api.Services;

public class RagMatchService : IRagMatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IResumeVectorStoreService _vectorStore;
    private readonly IPromptRepository _promptRepository;
    private readonly IClaudeChatService _claudeChatService;

    public RagMatchService(
        IResumeVectorStoreService vectorStore,
        IPromptRepository promptRepository,
        IClaudeChatService claudeChatService)
    {
        _vectorStore = vectorStore;
        _promptRepository = promptRepository;
        _claudeChatService = claudeChatService;
    }

    public async Task<RagMatchResponse> MatchAsync(string jobDescription, CancellationToken cancellationToken = default)
    {
        var chunks = await _vectorStore.GetRelevantChunksAsync(jobDescription, topK: 3, cancellationToken);

        var systemPrompt = _promptRepository.GetRagMatchSystemPrompt();
        var userMessage = _promptRepository.BuildRagMatchUserMessage(jobDescription, chunks);

        var rawJson = await _claudeChatService.SendPromptAsync(systemPrompt, userMessage, cancellationToken);

        var parsed = JsonSerializer.Deserialize<LlmMatchResult>(rawJson, JsonOptions)
            ?? throw new InvalidOperationException("LLM returned an empty or unparseable response.");

        return new RagMatchResponse
        {
            MatchScore = parsed.MatchScore,
            Summary = parsed.Summary,
            Strengths = parsed.Strengths,
            Gaps = parsed.Gaps,
            RetrievedContext = [.. chunks]
        };
    }

    private sealed class LlmMatchResult
    {
        [JsonPropertyName("matchScore")] public int MatchScore { get; init; }
        [JsonPropertyName("summary")] public string Summary { get; init; } = "";
        [JsonPropertyName("strengths")] public List<string> Strengths { get; init; } = [];
        [JsonPropertyName("gaps")] public List<string> Gaps { get; init; } = [];
    }
}
