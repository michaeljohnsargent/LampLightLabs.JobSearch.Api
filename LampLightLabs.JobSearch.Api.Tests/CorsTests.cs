using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace LampLightLabs.JobSearch.Api.Tests;

/// <summary>
/// Integration tests for the CORS policy registered in Program.cs.
///
/// The "ViteDev" policy (named for its original dev-only purpose; the name
/// wasn't updated when production origins were added) allows the Vite dev
/// server (http://localhost:5173) plus the production frontend's custom
/// domain (https://match.lamplightlabs.com) and its underlying Azure Static
/// Web Apps origin (https://blue-coast-05842a60f.7.azurestaticapps.net).
/// Tests verify that the Access-Control-Allow-Origin header is present for
/// allowed origins and absent for all others, and that browser preflight
/// OPTIONS requests are handled correctly.
/// </summary>
public class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // HTTPS base address avoids UseHttpsRedirection stripping CORS headers mid-redirect.
    // AllowAutoRedirect = false ensures we assert on the first response, not the redirect target.
    private HttpClient CreateTestClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false
        });

    [Fact]
    public async Task AllowedOrigin_SimpleRequest_IncludesAccessControlAllowOriginHeader()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        var ct = TestContext.Current.CancellationToken;

        // Act — any endpoint; CORS middleware adds headers regardless of auth outcome
        var response = await client.GetAsync("/api/v2/applications/fromcsv", ct);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(
            "http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task AllowedOrigin_ProductionCustomDomain_IncludesAccessControlAllowOriginHeader()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Add("Origin", "https://match.lamplightlabs.com");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await client.GetAsync("/api/v2/applications/fromcsv", ct);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(
            "https://match.lamplightlabs.com",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task AllowedOrigin_ProductionStaticWebAppOrigin_IncludesAccessControlAllowOriginHeader()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Add("Origin", "https://blue-coast-05842a60f.7.azurestaticapps.net");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await client.GetAsync("/api/v2/applications/fromcsv", ct);

        // Assert
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(
            "https://blue-coast-05842a60f.7.azurestaticapps.net",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task DisallowedOrigin_SimpleRequest_OmitsAccessControlAllowOriginHeader()
    {
        // Arrange
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Add("Origin", "http://evil.com");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await client.GetAsync("/api/v2/applications/fromcsv", ct);

        // Assert — origin not in the allow-list gets no CORS header
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AllowedOrigin_PreflightRequest_ReturnsNoContentWithCorsHeaders()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/token");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type,Authorization");

        // Act
        var response = await client.SendAsync(request, ct);

        // Assert — CORS middleware short-circuits and handles preflight directly
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(
            "http://localhost:5173",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task AllowedOrigin_ProductionCustomDomain_PreflightRequest_ReturnsNoContentWithCorsHeaders()
    {
        // Arrange — mirrors the browser preflight the live "Analyze match" button sends
        // from https://match.lamplightlabs.com before the actual POST to /api/rag/match.
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/rag/match");
        request.Headers.Add("Origin", "https://match.lamplightlabs.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await client.SendAsync(request, ct);

        // Assert — CORS middleware short-circuits and handles preflight directly
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(
            "https://match.lamplightlabs.com",
            response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }
}
