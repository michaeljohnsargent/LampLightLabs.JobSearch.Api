using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Integration tests for the three rate limiting policies registered in Program.cs.
///
/// Each test creates its own WebApplicationFactory with overridden limits so that
/// limits can be hit with just 2-3 requests and state does not bleed between tests.
/// (In-process test requests have a null RemoteIpAddress, so the partition key falls
/// back to "ip:unknown" — a shared factory would contaminate subsequent tests.)
/// </summary>
public class RateLimitingTests
{
    private static WebApplicationFactory<Program> CreateFactory(
        Dictionary<string, string?> configOverrides) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(configOverrides)));

    private static HttpClient CreateTestClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false
        });

    // --- Fixed window (auth endpoints) ---

    [Fact]
    public async Task FixedWindow_AuthEndpoint_ExceedingLimitReturns429()
    {
        // Arrange — PermitLimit=2, so the third request triggers 429
        await using var factory = CreateFactory(new()
        {
            ["RateLimiting:FixedWindow:PermitLimit"] = "2",
            ["RateLimiting:FixedWindow:WindowSeconds"] = "60"
        });
        var client = CreateTestClient(factory);
        var ct = TestContext.Current.CancellationToken;

        // Fresh content per call — HttpContent streams cannot be reused across sends
        static HttpContent MakeBody() =>
            JsonContent.Create(new { username = "demo", password = "wrong" });

        // Act — first two pass the rate limiter (bad creds → 401, but not 429)
        for (var i = 0; i < 2; i++)
        {
            var r = await client.PostAsync("/api/v1/auth/token", MakeBody(), ct);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode);
        }

        // Act — third request exceeds the fixed window
        var response = await client.PostAsync("/api/v1/auth/token", MakeBody(), ct);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task FixedWindow_RejectedResponse_IncludesRetryAfterHeader()
    {
        // Arrange — PermitLimit=1 so the second request is rejected
        await using var factory = CreateFactory(new()
        {
            ["RateLimiting:FixedWindow:PermitLimit"] = "1",
            ["RateLimiting:FixedWindow:WindowSeconds"] = "60"
        });
        var client = CreateTestClient(factory);
        var ct = TestContext.Current.CancellationToken;

        static HttpContent MakeBody() =>
            JsonContent.Create(new { username = "demo", password = "wrong" });

        // Exhaust the single permit
        await client.PostAsync("/api/v1/auth/token", MakeBody(), ct);

        // Act
        var response = await client.PostAsync("/api/v1/auth/token", MakeBody(), ct);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Retry-After"),
            "Rejected response should include a Retry-After header.");
    }

    // --- Sliding window (data endpoints) ---

    [Fact]
    public async Task SlidingWindow_DataEndpoint_ExceedingLimitReturns429()
    {
        // Arrange — PermitLimit=2, so the third request triggers 429
        await using var factory = CreateFactory(new()
        {
            ["RateLimiting:SlidingWindow:PermitLimit"] = "2",
            ["RateLimiting:SlidingWindow:WindowSeconds"] = "60",
            ["RateLimiting:SlidingWindow:SegmentsPerWindow"] = "4"
        });
        var client = CreateTestClient(factory);
        var ct = TestContext.Current.CancellationToken;

        // Act — first two pass (CSV may not exist in test env → 404, but not 429)
        for (var i = 0; i < 2; i++)
        {
            var r = await client.GetAsync("/api/v1/applications/fromcsv", ct);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode);
        }

        // Act — third request exceeds the window
        var response = await client.GetAsync("/api/v1/applications/fromcsv", ct);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    // --- Token bucket (AI/LLM endpoints) ---

    [Fact]
    public async Task TokenBucket_AiEndpoint_ExceedingBurstLimitReturns429()
    {
        // Arrange — TokenLimit=2, QueueLimit=0 so the third request is immediately rejected
        await using var factory = CreateFactory(new()
        {
            ["RateLimiting:TokenBucket:TokenLimit"] = "2",
            ["RateLimiting:TokenBucket:TokensPerPeriod"] = "2",
            ["RateLimiting:TokenBucket:ReplenishmentPeriodSeconds"] = "60",
            ["RateLimiting:TokenBucket:QueueLimit"] = "0"
        });
        var client = CreateTestClient(factory);
        var ct = TestContext.Current.CancellationToken;

        // An empty prompt returns 400 from the action itself, so the real AI service
        // is never called — rate limiting is middleware and fires before the action runs.
        static HttpContent MakeBody() =>
            JsonContent.Create(new { prompt = "" });

        // Act — first two requests are within the token budget (empty prompt → 400, not 429)
        for (var i = 0; i < 2; i++)
        {
            var r = await client.PostAsync("/api/v2/ai/chat", MakeBody(), ct);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode);
        }

        // Act — third request exhausts the bucket
        var response = await client.PostAsync("/api/v2/ai/chat", MakeBody(), ct);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
