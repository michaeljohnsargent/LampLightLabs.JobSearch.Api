using LampLightLabs.JobSearch.Api.Data;
using LampLightLabs.JobSearch.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// EF Core-backed usage tracking (Postgres in production via <see cref="JobSearchDbContext"/>).
/// Must be registered Scoped, matching the DbContext it depends on — see the captive-dependency
/// note on <see cref="EfJobStore"/>, which this follows the same shape as.
/// </summary>
public class UsageTrackingService : IUsageTrackingService
{
    private readonly JobSearchDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsageTrackingService> _logger;

    /// <summary>
    /// Logger defaults to <see cref="NullLogger{T}"/> when not supplied so existing
    /// <c>new UsageTrackingService(db, configuration)</c> call sites in tests keep compiling.
    /// </summary>
    public UsageTrackingService(
        JobSearchDbContext db,
        IConfiguration configuration,
        ILogger<UsageTrackingService>? logger = null)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger ?? NullLogger<UsageTrackingService>.Instance;
    }

    public async Task LogUsageAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var cost = _configuration.GetValue("UsageTracking:EstimatedCostPerCallUsd", 0.05m);
        _db.UsageLogs.Add(new UsageLog
        {
            Timestamp = DateTime.UtcNow,
            Endpoint = endpoint,
            EstimatedCostUsd = cost
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UsageSummary> GetCurrentMonthSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalCost = await _db.UsageLogs
            .Where(u => u.Timestamp >= monthStart)
            .SumAsync(u => u.EstimatedCostUsd, cancellationToken);

        var monthlyBudget = _configuration.GetValue("UsageTracking:MonthlyBudgetUsd", 10m);
        var hardCeiling = _configuration.GetValue("UsageTracking:MonthlyHardCeilingUsd", 15m);

        var percentOfBudget = monthlyBudget > 0 ? totalCost / monthlyBudget * 100m : 0m;
        var hasHitHardCeiling = totalCost >= hardCeiling;

        return new UsageSummary(totalCost, percentOfBudget, hasHitHardCeiling);
    }

    public async Task<bool> ShouldServeDemoAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration.GetValue("UsageTracking:DemoModeOnly", true))
            return true;

        try
        {
            var summary = await GetCurrentMonthSummaryAsync(cancellationToken);
            return summary.HasHitHardCeiling;
        }
        catch (Exception ex)
        {
            // Fail closed: if we can't confirm we're under budget, serve demo rather than risk
            // spend — the whole point of this service is a cost safety net.
            _logger.LogWarning(ex, "Failed to check monthly usage summary; failing closed to demo mode");
            return true;
        }
    }
}
