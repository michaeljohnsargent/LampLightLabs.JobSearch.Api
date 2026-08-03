namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>Current-month usage vs. the configured budget/ceiling.</summary>
public record UsageSummary(decimal TotalCostUsd, decimal PercentOfBudgetUsed, bool HasHitHardCeiling);

public interface IUsageTrackingService
{
    /// <summary>Logs one real pipeline call's estimated cost against <paramref name="endpoint"/>.</summary>
    Task LogUsageAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>Sums this calendar month's (UTC) logged cost against the configured budget/ceiling.</summary>
    Task<UsageSummary> GetCurrentMonthSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True if a request should be short-circuited to a demo response instead of running the
    /// real pipeline — either because <c>DemoModeOnly</c> is on, or the monthly hard ceiling has
    /// been reached (the circuit breaker).
    /// </summary>
    Task<bool> ShouldServeDemoAsync(CancellationToken cancellationToken = default);
}
