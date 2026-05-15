using LampLightLabs.JobSearch.Api.Controllers.V1;
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

public class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Standalone config for unit tests - no factory involvement.
    private static IConfiguration BuildTestConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-test-key-that-is-at-least-32-chars!!",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

    public AuthenticationTests(WebApplicationFactory<Program> factory)
    {
        // Use the plain factory with no config overrides.
        // Both the JWT bearer middleware and ITokenService read from the same
        // appsettings.json at runtime, so tokens generated via ITokenService
        // will always pass middleware validation.
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

    // --- TokenService Unit Tests ---

    [Fact]
    public void GenerateToken_ValidUsername_ReturnsNonEmptyToken()
    {
        // Arrange
        var service = new TokenService(BuildTestConfig());

        // Act
        var token = service.GenerateToken("testuser");

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateToken_ValidUsername_TokenContainsUsernameClaim()
    {
        // Arrange
        var service = new TokenService(BuildTestConfig());

        // Act
        var tokenString = service.GenerateToken("testuser");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        Assert.Contains(token.Claims, c => c.Value == "testuser");
    }

    [Fact]
    public void GenerateToken_ValidUsername_TokenExpiryIsInFuture()
    {
        // Arrange
        var service = new TokenService(BuildTestConfig());

        // Act
        var tokenString = service.GenerateToken("testuser");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_MissingJwtKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var service = new TokenService(config);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.GenerateToken("testuser"));
    }

    // --- AuthController Unit Tests ---

    [Fact]
    public void GetToken_ValidCredentials_Returns200WithToken()
    {
        // Arrange
        var mockTokenService = new Mock<ITokenService>();
        mockTokenService.Setup(s => s.GenerateToken("demo")).Returns("mock-token");
        var controller = new AuthController(mockTokenService.Object);
        var request = new LoginRequest { Username = "demo", Password = "password" };

        // Act
        var result = controller.GetToken(request) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var response = result.Value as TokenResponse;
        Assert.NotNull(response);
        Assert.Equal("mock-token", response.Token);
    }

    [Fact]
    public void GetToken_InvalidPassword_Returns401()
    {
        // Arrange
        var mockTokenService = new Mock<ITokenService>();
        var controller = new AuthController(mockTokenService.Object);
        var request = new LoginRequest { Username = "demo", Password = "wrongpassword" };

        // Act
        var result = controller.GetToken(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void GetToken_InvalidUsername_Returns401()
    {
        // Arrange
        var mockTokenService = new Mock<ITokenService>();
        var controller = new AuthController(mockTokenService.Object);
        var request = new LoginRequest { Username = "wronguser", Password = "password" };

        // Act
        var result = controller.GetToken(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void GetToken_EmptyCredentials_Returns401()
    {
        // Arrange
        var mockTokenService = new Mock<ITokenService>();
        var controller = new AuthController(mockTokenService.Object);
        var request = new LoginRequest { Username = "", Password = "" };

        // Act
        var result = controller.GetToken(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    // --- Integration Tests ---

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await client.GetAsync("/api/v2/applications/fromcsv", ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_AuthorizationAccepted()
    {
        // Arrange
        var client = CreateTestClient();
        var ct = TestContext.Current.CancellationToken;

        // Resolve ITokenService from the running app's DI container.
        // This guarantees the token is signed with the exact same key, issuer,
        // and audience that the JWT bearer middleware uses for validation.
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokenService.GenerateToken("demo");

        // Act
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/v2/applications/fromcsv", ct);

        // Assert - auth accepted (not 401). CSV may not exist in test env so 404 is acceptable.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
