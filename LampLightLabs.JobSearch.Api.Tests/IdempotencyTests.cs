using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Tests for idempotent POST /api/v2/applications.
///
/// Four cases prove the full contract:
///   1. New key        -> 201, record created.
///   2. Same key, same body  -> 201 replayed, no duplicate created.
///   3. Missing key    -> 400.
///   4. Same key, different body -> 422, key reuse on different payload is rejected.
/// </summary>
public class IdempotencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IdempotencyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // Use HTTPS base address so UseHttpsRedirection does not strip the Authorization header.
    private HttpClient CreateTestClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

    /// <summary>
    /// Resolves ITokenService from the running app's DI container and generates
    /// a client credentials JWT. This guarantees the token is signed with the
    /// same key, issuer, and audience the JWT bearer middleware expects.
    /// </summary>
    private string GetBearerToken()
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerateClientToken("test-client", "api.read");
    }

    private static HttpContent BuildBody(string company = "Acme Corp") =>
        JsonContent.Create(new
        {
            Company = company,
            Role = "Senior .NET Engineer",
            Platform = "Dice",
            ContactName = "Jane Recruiter",
            DateApplied = "2026-05-25",
            RateBudget = "$85-90/hr W2",
            Notes = "Strong stack match.",
            LinkToJobPost = "https://dice.com/jobs/123"
        });

    // --- Test 1: New key returns 201 ---

    [Fact]
    public async Task Post_NewIdempotencyKey_Returns201()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GetBearerToken());

        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/applications")
        {
            Content = BuildBody(),
            Headers = { { "Idempotency-Key", idempotencyKey } }
        };

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // --- Test 2: Same key, same body replays the cached 201 ---

    [Fact]
    public async Task Post_SameKeyAndBody_ReturnsCached201WithoutDuplicate()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GetBearerToken());

        var idempotencyKey = Guid.NewGuid().ToString();

        HttpRequestMessage MakeRequest() => new HttpRequestMessage(HttpMethod.Post, "/api/v2/applications")
        {
            Content = BuildBody("Replay Corp"),
            Headers = { { "Idempotency-Key", idempotencyKey } }
        };

        // Act - first call creates the record
        var first = await client.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Act - second call with the same key and body should replay the same 201
        var second = await client.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        // Both responses should contain the same company name, confirming replay not re-creation.
        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(firstBody, secondBody);
    }

    // --- Test 3: Missing Idempotency-Key header returns 400 ---

    [Fact]
    public async Task Post_MissingIdempotencyKey_Returns400()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GetBearerToken());

        // No Idempotency-Key header
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/applications")
        {
            Content = BuildBody()
        };

        // Act
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Test 4: Same key, different body returns 422 ---

    [Fact]
    public async Task Post_SameKeyDifferentBody_Returns422()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GetBearerToken());

        var idempotencyKey = Guid.NewGuid().ToString();

        var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/applications")
        {
            Content = BuildBody("Original Company"),
            Headers = { { "Idempotency-Key", idempotencyKey } }
        };

        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/applications")
        {
            Content = BuildBody("Different Company"),  // same key, different payload
            Headers = { { "Idempotency-Key", idempotencyKey } }
        };

        // Act - first call succeeds
        var first = await client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Act - second call reuses the key on a different body
        var second = await client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        // Assert - server rejects the mismatch
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }
}
