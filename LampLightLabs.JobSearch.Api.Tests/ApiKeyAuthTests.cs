using LampLightLabs.JobSearch.Api.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace LampLightLabs.JobSearch.Api.Tests;

public class ApiKeyAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private const string ValidTestApiKey = "test-api-key-for-unit-tests";

    public ApiKeyAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // Builds an AuthorizationFilterContext with the given Authorization header value
    // and a DI container that returns the specified API key from configuration.
    private static AuthorizationFilterContext BuildFilterContext(string authHeaderValue, string configuredApiKey)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = authHeaderValue;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiKey:Key"] = configuredApiKey
                })
                .Build());
        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    // --- ApiKeyAuthAttribute Unit Tests ---

    [Fact]
    public void OnAuthorization_ValidKey_AuthenticatesRequest()
    {
        // Arrange
        var context = BuildFilterContext($"ApiKey {ValidTestApiKey}", ValidTestApiKey);
        var attribute = new ApiKeyAuthAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert - no result set means the request was allowed through
        Assert.Null(context.Result);
        Assert.True(context.HttpContext.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public void OnAuthorization_InvalidKey_Returns401()
    {
        // Arrange
        var context = BuildFilterContext("ApiKey wrong-key", ValidTestApiKey);
        var attribute = new ApiKeyAuthAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void OnAuthorization_MissingAuthorizationHeader_Returns401()
    {
        // Arrange
        var context = BuildFilterContext(string.Empty, ValidTestApiKey);
        var attribute = new ApiKeyAuthAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void OnAuthorization_WrongScheme_Returns401()
    {
        // Arrange - sends Bearer token instead of ApiKey scheme
        var context = BuildFilterContext($"Bearer {ValidTestApiKey}", ValidTestApiKey);
        var attribute = new ApiKeyAuthAttribute();

        // Act
        attribute.OnAuthorization(context);

        // Assert
        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    // --- Integration Tests ---

    private HttpClient CreateTestClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

    private string GetConfiguredApiKey()
    {
        using var scope = _factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        return config["ApiKey:Key"]!;
    }

    [Fact]
    public async Task ApiKeyEndpoint_WithoutApiKey_Returns401()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await client.GetAsync("/api/v2/applications/status", ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyEndpoint_WithValidApiKey_Returns200()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;
        var apiKey = GetConfiguredApiKey();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);

        // Act
        var response = await client.GetAsync("/api/v2/applications/status", ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyEndpoint_WithInvalidApiKey_Returns401()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", "wrong-key");

        // Act
        var response = await client.GetAsync("/api/v2/applications/status", ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyEndpoint_WithJwtToken_Returns401()
    {
        // Arrange - JWT Bearer token does not satisfy ApiKey authentication
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<
            LampLightLabs.JobSearch.Api.Services.ITokenService>();
        var jwtToken = tokenService.GenerateToken("demo");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

        // Act
        var response = await client.GetAsync("/api/v2/applications/status", ct);

        // Assert - JWT is not valid for API key protected endpoints
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
