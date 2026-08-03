namespace LampLightLabs.JobSearch.Api.Models.Rag;

public class UsageSummaryResponse
{
    public decimal TotalCostUsd { get; init; }
    public decimal PercentOfBudgetUsed { get; init; }
    public bool HasHitHardCeiling { get; init; }
}
