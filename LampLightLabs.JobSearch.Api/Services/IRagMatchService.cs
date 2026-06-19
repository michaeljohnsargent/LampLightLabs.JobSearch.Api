using LampLightLabs.JobSearch.Api.Models.Rag;

namespace LampLightLabs.JobSearch.Api.Services;

public interface IRagMatchService
{
    Task<RagMatchResponse> MatchAsync(string jobDescription, CancellationToken cancellationToken = default);
}
