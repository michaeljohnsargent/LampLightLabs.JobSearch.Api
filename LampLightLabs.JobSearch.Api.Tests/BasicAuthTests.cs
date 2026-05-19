using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace LampLightLabs.JobSearch.Api.Tests;

public class BasicAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BasicAuthTests(WebApplicationFactory<Program> factory)
    {
        // Override configuration so BasicAuthHandler reads the expected test credentials.
        // Credentials injected here must match what each test encodes in the Authorization header.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BasicAuth:Username"] = "testuser",
                    ["BasicAuth:Password"] = "testpassword"
                });
            });
        });
    }

    // Use HTTPS base address so UseHttpsRedirection does not fire.
    // HttpClient strips the Authorization header when following an HTTP-to-HTTPS redirect,
    // which produces false 401s on protected endpoints.
    private HttpClient CreateTestClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/")
        });

    // --- Happy path ---

    [Fact]
    public async Task ProtectedEndpoint_ValidBasicAuth_ReturnsSuccess()
    {
        var client = CreateTestClient();
        var byteArray = Encoding.ASCII.GetBytes("testuser:testpassword");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    // --- Credential mismatch ---

    [Fact]
    public async Task ProtectedEndpoint_WrongCredentials_ReturnsUnauthorized()
    {
        var client = CreateTestClient();
        var byteArray = Encoding.ASCII.GetBytes("invaliduser:invalidpassword");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Missing / malformed header ---

    [Fact]
    public async Task ProtectedEndpoint_MissingAuthorizationHeader_ReturnsUnauthorized()
    {
        var client = CreateTestClient();

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_InvalidBase64_ReturnsUnauthorized()
    {
        var client = CreateTestClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "not-valid-base64!");

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Credential format edge cases ---

    [Fact]
    public async Task ProtectedEndpoint_MissingColon_ReturnsUnauthorized()
    {
        var client = CreateTestClient();
        var byteArray = Encoding.ASCII.GetBytes("testuserpassword");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ExtraColons_ReturnsUnauthorized()
    {
        // Handler splits on the first colon only — "testuser" / "password:extra".
        // Neither segment matches the configured credentials so 401 is expected.
        var client = CreateTestClient();
        var byteArray = Encoding.ASCII.GetBytes("testuser:password:extra");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_EmptyUsernameAndPassword_ReturnsUnauthorized()
    {
        var client = CreateTestClient();
        var byteArray = Encoding.ASCII.GetBytes(":");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhitespaceCredentials_ReturnsUnauthorized()
    {
        // Whitespace-only username and password do not match configured credentials.
        var client = CreateTestClient();
        var byteArray = Encoding.ASCII.GetBytes("   :   ");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_LongCredentials_ReturnsUnauthorized()
    {
        var client = CreateTestClient();
        var byteArray = Encoding.ASCII.GetBytes($"{new string('u', 100)}:{new string('p', 100)}");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_UnicodeCredentials_ReturnsUnauthorized()
    {
        // UTF-8 encoded credentials that do not match the ASCII-configured values.
        var client = CreateTestClient();
        var byteArray = Encoding.UTF8.GetBytes("tëstuser:pässwörd");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        var response = await client.GetAsync("/api/v2/Applications/count", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
