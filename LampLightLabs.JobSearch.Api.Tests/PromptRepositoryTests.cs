using LampLightLabs.JobSearch.Api.Services;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Tests for <see cref="PromptRepository"/>.
///
/// No mocks — these verify that the prompt content meets the structural
/// requirements that make the RAG pipeline work correctly.
/// </summary>
public class PromptRepositoryTests
{
    private readonly PromptRepository _sut = new();

    // --- System prompt ---

    [Fact]
    public void GetRagMatchSystemPrompt_IsNotNullOrEmpty()
    {
        var result = _sut.GetRagMatchSystemPrompt();

        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void GetRagMatchSystemPrompt_InstructsJsonOnlyOutput()
    {
        var result = _sut.GetRagMatchSystemPrompt();

        Assert.Contains("JSON", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("matchScore", result);
        Assert.Contains("summary", result);
        Assert.Contains("strengths", result);
        Assert.Contains("gaps", result);
    }

    [Fact]
    public void GetRagMatchSystemPrompt_ProhibitsMarkdownFences()
    {
        var result = _sut.GetRagMatchSystemPrompt();

        Assert.Contains("No markdown", result, StringComparison.OrdinalIgnoreCase);
    }

    // --- User message builder ---

    [Fact]
    public void BuildRagMatchUserMessage_ContainsJobDescription()
    {
        var jd = "We are looking for a Senior .NET Engineer with Azure experience.";

        var result = _sut.BuildRagMatchUserMessage(jd, ["chunk A"]);

        Assert.Contains(jd, result);
    }

    [Fact]
    public void BuildRagMatchUserMessage_ContainsAllResumeChunks()
    {
        var chunks = new[] { "Skills section text", "Experience section text" };

        var result = _sut.BuildRagMatchUserMessage("some job", chunks);

        foreach (var chunk in chunks)
            Assert.Contains(chunk, result);
    }

    [Fact]
    public void BuildRagMatchUserMessage_IsNotNullOrEmpty()
    {
        var result = _sut.BuildRagMatchUserMessage("job description", ["chunk"]);

        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
