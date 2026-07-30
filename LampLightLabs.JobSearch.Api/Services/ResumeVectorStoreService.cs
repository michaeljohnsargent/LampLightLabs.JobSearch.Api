using System.ClientModel;
using Microsoft.Extensions.AI;

namespace LampLightLabs.JobSearch.Api.Services;

public class ResumeVectorStoreService : BackgroundService, IResumeVectorStoreService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly List<(string Chunk, ReadOnlyMemory<float> Embedding)> _store = [];
    private readonly TaskCompletionSource _initialized = new();

    public ResumeVectorStoreService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var chunks = LoadResumeChunks();
            var embeddings = await _embeddingGenerator.GenerateAsync(chunks, cancellationToken: stoppingToken);
            for (var i = 0; i < chunks.Count; i++)
                _store.Add((chunks[i], embeddings[i].Vector));
            _initialized.SetResult();
        }
        catch (Exception ex)
        {
            _initialized.SetException(ex);
        }
    }

    public async Task<IReadOnlyList<string>> GetRelevantChunksAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        ReadOnlyMemory<float> queryEmbedding;
        try
        {
            await _initialized.Task.WaitAsync(cancellationToken);

            var result = await _embeddingGenerator.GenerateAsync([query], cancellationToken: cancellationToken);
            queryEmbedding = result[0].Vector;
        }
        catch (ClientResultException ex)
        {
            throw OpenAiExceptionTranslator.Translate(ex);
        }

        return [.. _store
            .Select(entry => (entry.Chunk, Score: CosineSimilarity(queryEmbedding, entry.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)];
    }

    private static List<string> LoadResumeChunks()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ResumeData", "resume.txt");
        var content = File.ReadAllText(path);
        return [.. content
            .Split("---", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk))];
    }

    private static float CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        var sa = a.Span;
        var sb = b.Span;
        float dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < sa.Length; i++)
        {
            dot += sa[i] * sb[i];
            magA += sa[i] * sa[i];
            magB += sb[i] * sb[i];
        }
        return dot / (MathF.Sqrt(magA) * MathF.Sqrt(magB));
    }
}
