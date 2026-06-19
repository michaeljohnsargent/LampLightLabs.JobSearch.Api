using LampLightLabs.JobSearch.Api.Services;
using Moq;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Unit tests for <see cref="RagMatchService"/>.
///
/// All three dependencies are mocked so these tests verify orchestration logic
/// only: chunk retrieval is passed to the prompt builder, the LLM response is
/// parsed into the response model, and retrieved chunks land in RetrievedContext.
/// </summary>
public class RagMatchServiceTests
{
    private const string JobDescription = "Senior .NET Engineer with Azure experience";

    private const string ValidLlmJson = """
        {
          "matchScore": 78,
          "summary": "Solid backend fit with strong .NET and Azure credentials.",
          "strengths": ["ASP.NET Core", "Azure DevOps"],
          "gaps": ["Kubernetes", "Go"]
        }
        """;

    private static (Mock<IResumeVectorStoreService> vectorStore, Mock<IPromptRepository> prompts, Mock<IClaudeChatService> claude, RagMatchService sut)
        BuildSut(IReadOnlyList<string>? chunks = null, string? llmJson = null)
    {
        var vectorStore = new Mock<IResumeVectorStoreService>();
        vectorStore
            .Setup(v => v.GetRelevantChunksAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks ?? ["chunk A", "chunk B", "chunk C"]);

        var prompts = new Mock<IPromptRepository>();
        prompts.Setup(p => p.GetRagMatchSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildRagMatchUserMessage(It.IsAny<string>(), It.IsAny<IEnumerable<string>>())).Returns("user message");

        var claude = new Mock<IClaudeChatService>();
        claude
            .Setup(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmJson ?? ValidLlmJson);

        var sut = new RagMatchService(vectorStore.Object, prompts.Object, claude.Object);
        return (vectorStore, prompts, claude, sut);
    }

    // --- LLM response parsing ---

    [Fact]
    public async Task MatchAsync_ParsesMatchScoreFromLlm()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _, _, sut) = BuildSut();

        var result = await sut.MatchAsync(JobDescription, ct);

        Assert.Equal(78, result.MatchScore);
    }

    [Fact]
    public async Task MatchAsync_ParsesSummaryFromLlm()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _, _, sut) = BuildSut();

        var result = await sut.MatchAsync(JobDescription, ct);

        Assert.Equal("Solid backend fit with strong .NET and Azure credentials.", result.Summary);
    }

    [Fact]
    public async Task MatchAsync_ParsesStrengthsAndGapsFromLlm()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _, _, sut) = BuildSut();

        var result = await sut.MatchAsync(JobDescription, ct);

        Assert.Equal(["ASP.NET Core", "Azure DevOps"], result.Strengths);
        Assert.Equal(["Kubernetes", "Go"], result.Gaps);
    }

    // --- RetrievedContext injection ---

    [Fact]
    public async Task MatchAsync_RetrievedContextComesFromVectorStore_NotLlm()
    {
        var ct = TestContext.Current.CancellationToken;
        var expectedChunks = new List<string> { "Skills section", "Experience section" };
        var (_, _, _, sut) = BuildSut(chunks: expectedChunks);

        var result = await sut.MatchAsync(JobDescription, ct);

        Assert.Equal(expectedChunks, result.RetrievedContext);
    }

    // --- Orchestration wiring ---

    [Fact]
    public async Task MatchAsync_PassesJobDescriptionToVectorStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var (vectorStore, _, _, sut) = BuildSut();

        await sut.MatchAsync(JobDescription, ct);

        vectorStore.Verify(v => v.GetRelevantChunksAsync(JobDescription, 3, ct), Times.Once);
    }

    [Fact]
    public async Task MatchAsync_PassesSystemPromptAndUserMessageToClaude()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, prompts, claude, sut) = BuildSut();
        prompts.Setup(p => p.GetRagMatchSystemPrompt()).Returns("my-system");
        prompts.Setup(p => p.BuildRagMatchUserMessage(It.IsAny<string>(), It.IsAny<IEnumerable<string>>())).Returns("my-user-msg");

        await sut.MatchAsync(JobDescription, ct);

        claude.Verify(c => c.SendPromptAsync("my-system", "my-user-msg", ct), Times.Once);
    }

    [Fact]
    public async Task MatchAsync_PassesChunksToPromptBuilder()
    {
        var ct = TestContext.Current.CancellationToken;
        var chunks = new List<string> { "chunk X", "chunk Y" };
        var (_, prompts, _, sut) = BuildSut(chunks: chunks);

        await sut.MatchAsync(JobDescription, ct);

        prompts.Verify(p => p.BuildRagMatchUserMessage(
            JobDescription,
            It.Is<IEnumerable<string>>(c => c.SequenceEqual(chunks))), Times.Once);
    }
}
