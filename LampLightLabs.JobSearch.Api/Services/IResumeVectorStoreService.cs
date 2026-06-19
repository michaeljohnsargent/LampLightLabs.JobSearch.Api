namespace LampLightLabs.JobSearch.Api.Services;

public interface IResumeVectorStoreService
{
    Task<IReadOnlyList<string>> GetRelevantChunksAsync(string query, int topK, CancellationToken cancellationToken = default);
}
