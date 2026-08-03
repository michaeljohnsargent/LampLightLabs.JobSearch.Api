using LampLightLabs.JobSearch.Api.Data;
using LampLightLabs.JobSearch.Api.Models;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LampLightLabs.JobSearch.Api.Tests
{
    /// <summary>
    /// Tests for UsageTrackingService — verifies budget/ceiling math and the demo-toggle/circuit-
    /// breaker decision using the EF Core InMemory provider (same pattern as EfJobStoreTests).
    /// Each test builds its own uniquely-named database so tests don't share state.
    /// </summary>
    public class UsageTrackingServiceTests
    {
        private static JobSearchDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<JobSearchDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new JobSearchDbContext(options);
        }

        private static IConfiguration NewConfig(
            decimal monthlyBudget = 10m,
            decimal hardCeiling = 15m,
            decimal costPerCall = 0.05m,
            bool demoModeOnly = false)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UsageTracking:MonthlyBudgetUsd"] = monthlyBudget.ToString(),
                    ["UsageTracking:MonthlyHardCeilingUsd"] = hardCeiling.ToString(),
                    ["UsageTracking:EstimatedCostPerCallUsd"] = costPerCall.ToString(),
                    ["UsageTracking:DemoModeOnly"] = demoModeOnly.ToString()
                })
                .Build();
        }

        // --- LogUsageAsync ---

        [Fact]
        public async Task LogUsageAsync_InsertsRowWithConfiguredCost()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            var service = new UsageTrackingService(db, NewConfig(costPerCall: 0.25m));

            await service.LogUsageAsync("api/rag/match", ct);

            var log = Assert.Single(db.UsageLogs);
            Assert.Equal("api/rag/match", log.Endpoint);
            Assert.Equal(0.25m, log.EstimatedCostUsd);
        }

        // --- GetCurrentMonthSummaryAsync ---

        [Fact]
        public async Task GetCurrentMonthSummaryAsync_SumsOnlyCurrentMonthLogs()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            db.UsageLogs.AddRange(
                new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 1m, Endpoint = "api/rag/match" },
                new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 2m, Endpoint = "api/rag/match" },
                new UsageLog { Timestamp = DateTime.UtcNow.AddMonths(-1), EstimatedCostUsd = 100m, Endpoint = "api/rag/match" });
            await db.SaveChangesAsync(ct);
            var service = new UsageTrackingService(db, NewConfig());

            var summary = await service.GetCurrentMonthSummaryAsync(ct);

            Assert.Equal(3m, summary.TotalCostUsd);
        }

        [Fact]
        public async Task GetCurrentMonthSummaryAsync_ComputesPercentOfBudgetUsed()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            db.UsageLogs.Add(new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 5m, Endpoint = "api/rag/match" });
            await db.SaveChangesAsync(ct);
            var service = new UsageTrackingService(db, NewConfig(monthlyBudget: 10m));

            var summary = await service.GetCurrentMonthSummaryAsync(ct);

            Assert.Equal(50m, summary.PercentOfBudgetUsed);
        }

        [Fact]
        public async Task GetCurrentMonthSummaryAsync_JustUnderHardCeiling_HasHitHardCeilingIsFalse()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            db.UsageLogs.Add(new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 14.99m, Endpoint = "api/rag/match" });
            await db.SaveChangesAsync(ct);
            var service = new UsageTrackingService(db, NewConfig(hardCeiling: 15m));

            var summary = await service.GetCurrentMonthSummaryAsync(ct);

            Assert.False(summary.HasHitHardCeiling);
        }

        [Fact]
        public async Task GetCurrentMonthSummaryAsync_AtHardCeiling_HasHitHardCeilingIsTrue()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            db.UsageLogs.Add(new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 15m, Endpoint = "api/rag/match" });
            await db.SaveChangesAsync(ct);
            var service = new UsageTrackingService(db, NewConfig(hardCeiling: 15m));

            var summary = await service.GetCurrentMonthSummaryAsync(ct);

            Assert.True(summary.HasHitHardCeiling);
        }

        // --- ShouldServeDemoAsync (circuit breaker + demo toggle) ---

        [Fact]
        public async Task ShouldServeDemoAsync_DemoModeOnlyTrue_ReturnsTrueEvenAtZeroUsage()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            var service = new UsageTrackingService(db, NewConfig(demoModeOnly: true));

            Assert.True(await service.ShouldServeDemoAsync(ct));
        }

        [Fact]
        public async Task ShouldServeDemoAsync_DemoModeOffAndUnderCeiling_ReturnsFalse()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            db.UsageLogs.Add(new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 5m, Endpoint = "api/rag/match" });
            await db.SaveChangesAsync(ct);
            var service = new UsageTrackingService(db, NewConfig(hardCeiling: 15m, demoModeOnly: false));

            Assert.False(await service.ShouldServeDemoAsync(ct));
        }

        [Fact]
        public async Task ShouldServeDemoAsync_DemoModeOffAndAtHardCeiling_ReturnsTrue()
        {
            // Circuit breaker firing at the exact threshold.
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            db.UsageLogs.Add(new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 15m, Endpoint = "api/rag/match" });
            await db.SaveChangesAsync(ct);
            var service = new UsageTrackingService(db, NewConfig(hardCeiling: 15m, demoModeOnly: false));

            Assert.True(await service.ShouldServeDemoAsync(ct));
        }

        [Fact]
        public async Task ShouldServeDemoAsync_DemoModeOffAndOverHardCeiling_ReturnsTrue()
        {
            var ct = TestContext.Current.CancellationToken;
            var db = NewContext();
            db.UsageLogs.Add(new UsageLog { Timestamp = DateTime.UtcNow, EstimatedCostUsd = 20m, Endpoint = "api/rag/match" });
            await db.SaveChangesAsync(ct);
            var service = new UsageTrackingService(db, NewConfig(hardCeiling: 15m, demoModeOnly: false));

            Assert.True(await service.ShouldServeDemoAsync(ct));
        }
    }
}
