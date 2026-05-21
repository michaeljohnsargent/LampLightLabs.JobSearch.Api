using LampLightLabs.JobSearch.Api.Controllers;
using LampLightLabs.JobSearch.Api.Models.Auth;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;

namespace LampLightLabs.JobSearch.Api.Tests;

public class OAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Standalone config for unit tests - includes OAuthClients section.
    private static IConfiguration BuildTestConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-test-key-that-is-at-least-32-chars!!",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = "60",
                ["OAuthClients:0:ClientId"] = "test-client",
                ["OAuthClients:0:ClientSecret"] = "test-secret",
                ["OAuthClients:0:AllowedScopes:0"] = "api.read"
            })
            .Build();

    public OAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // Use HTTPS base address so UseHttpsRedirection does not fire.
    // HttpClient strips Authorization headers when following HTTP-to-HTTPS
    // redirects, which causes false 401s on protected endpoints.
    private HttpClient CreateTestClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

    // --- TokenService Unit Tests (GenerateClientToken) ---

    [Fact]
    public void GenerateClientToken_ValidClientId_ReturnsNonEmptyToken()
    {
        // Arrange
        var service = new TokenService(BuildTestConfig());

        // Act
        var token = service.GenerateClientToken("test-client", "api.read");

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateClientToken_ValidClientId_TokenContainsClientIdClaim()
    {
        // Arrange
        var service = new TokenService(BuildTestConfig());

        // Act
        var tokenString = service.GenerateClientToken("test-client", "api.read");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        Assert.Contains(token.Claims, c => c.Type == "client_id" && c.Value == "test-client");
    }

    [Fact]
    public void GenerateClientToken_WithScope_TokenContainsScopeClaim()
    {
        // Arrange
        var service = new TokenService(BuildTestConfig());

        // Act
        var tokenString = service.GenerateClientToken("test-client", "api.read");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        Assert.Contains(token.Claims, c => c.Type == "scope" && c.Value == "api.read");
    }

    [Fact]
    public void GenerateClientToken_WithoutScope_NoScopeClaim()
    {
        // Arrange
        var service = new TokenService(BuildTestConfig());

        // Act
        var tokenString = service.GenerateClientToken("test-client", null);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        Assert.DoesNotContain(token.Claims, c => c.Type == "scope");
    }

    // --- OAuthClientService Unit Tests ---

    [Fact]
    public void ValidateClient_ValidCredentials_ReturnsTrue()
    {
        // Arrange
        var service = new OAuthClientService(BuildTestConfig());

        // Act & Assert
        Assert.True(service.ValidateClient("test-client", "test-secret"));
    }

    [Fact]
    public void ValidateClient_InvalidClientId_ReturnsFalse()
    {
        // Arrange
        var service = new OAuthClientService(BuildTestConfig());

        // Act & Assert
        Assert.False(service.ValidateClient("wrong-client", "test-secret"));
    }

    [Fact]
    public void ValidateClient_InvalidClientSecret_ReturnsFalse()
    {
        // Arrange
        var service = new OAuthClientService(BuildTestConfig());

        // Act & Assert
        Assert.False(service.ValidateClient("test-client", "wrong-secret"));
    }

    [Fact]
    public void ValidateClient_EmptyCredentials_ReturnsFalse()
    {
        // Arrange
        var service = new OAuthClientService(BuildTestConfig());

        // Act & Assert
        Assert.False(service.ValidateClient("", ""));
    }

    // --- OAuthController Unit Tests ---

    [Fact]
    public void GetToken_ValidCredentials_Returns200WithToken()
    {
        // Arrange
        var mockClientService = new Mock<IOAuthClientService>();
        mockClientService.Setup(s => s.ValidateClient("test-client", "test-secret")).Returns(true);
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(s => s.GenerateClientToken("test-client", "api.read")).Returns("mock-oauth-token");

        var controller = new OAuthController(mockClientService.Object, mockTokenService.Object);
        var request = new OAuthTokenRequest
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            GrantType = "client_credentials",
            Scope = "api.read"
        };

        // Act
        var result = controller.GetToken(request) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var response = result.Value as OAuthTokenResponse;
        Assert.NotNull(response);
        Assert.Equal("mock-oauth-token", response.AccessToken);
    }

    [Fact]
    public void GetToken_InvalidGrantType_Returns400()
    {
        // Arrange
        var mockClientService = new Mock<IOAuthClientService>();
        var mockTokenService = new Mock<ITokenService>();
        var controller = new OAuthController(mockClientService.Object, mockTokenService.Object);
        var request = new OAuthTokenRequest
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            GrantType = "password",
            Scope = "api.read"
        };

        // Act
        var result = controller.GetToken(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetToken_InvalidClientCredentials_Returns401()
    {
        // Arrange
        var mockClientService = new Mock<IOAuthClientService>();
        mockClientService
            .Setup(s => s.ValidateClient(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);
        var mockTokenService = new Mock<ITokenService>();
        var controller = new OAuthController(mockClientService.Object, mockTokenService.Object);
        var request = new OAuthTokenRequest
        {
            ClientId = "wrong-client",
            ClientSecret = "wrong-secret",
            GrantType = "client_credentials",
            Scope = "api.read"
        };

        // Act
        var result = controller.GetToken(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // --- Integration Tests ---

    [Fact]
    public async Task StatsEndpoint_WithoutToken_Returns401()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await client.GetAsync("/api/v2/applications/stats", ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StatsEndpoint_WithValidOAuthToken_AuthorizationAccepted()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        // Resolve ITokenService from the running app's DI container.
        // This guarantees the token is signed with the exact same key, issuer,
        // and audience that the JWT bearer middleware uses for validation.
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokenService.GenerateClientToken("jobsearch-client", "api.read");

        // Act
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/v2/applications/stats", ct);

        // Assert - auth accepted (not 401). CSV may not exist in test env so 404 is acceptable.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StatsEndpoint_WithGarbageToken_Returns401()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "this-is-not-a-valid-token");
        var response = await client.GetAsync("/api/v2/applications/stats", ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
