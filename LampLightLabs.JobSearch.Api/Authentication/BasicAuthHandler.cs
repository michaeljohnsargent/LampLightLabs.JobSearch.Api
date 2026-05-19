using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LampLightLabs.JobSearch.Api.Authentication;

/// <summary>
/// Handles HTTP Basic authentication by validating credentials supplied in the
/// Authorization header against values stored in application configuration.
/// </summary>
/// <remarks>
/// Expected header format: Authorization: Basic {base64(username:password)}
///
/// Credentials are read from configuration at BasicAuth:Username and BasicAuth:Password.
/// Use user secrets locally and environment variables in production —
/// never commit real credentials to source control.
/// </remarks>
public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BasicScheme = "Basic";

    /// <summary>
    /// Initializes a new instance of <see cref="BasicAuthHandler"/>.
    /// </summary>
    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// Attempts to authenticate the current request using Basic credentials.
    /// Returns <see cref="AuthenticateResult.Success"/> when credentials are valid,
    /// or <see cref="AuthenticateResult.Fail"/> for any malformed or invalid input.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        if (!authHeader.StartsWith($"{BasicScheme} ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Authorization header is not Basic."));
        }

        var encodedCredentials = authHeader[$"{BasicScheme} ".Length..].Trim();

        string decodedCredentials;
        try
        {
            var credentialBytes = Convert.FromBase64String(encodedCredentials);
            decodedCredentials = Encoding.UTF8.GetString(credentialBytes);
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Base64 in Authorization header."));
        }

        var separatorIndex = decodedCredentials.IndexOf(':');
        if (separatorIndex < 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid credential format."));
        }

        var username = decodedCredentials[..separatorIndex];
        var password = decodedCredentials[(separatorIndex + 1)..];

        var configuration = Context.RequestServices.GetRequiredService<IConfiguration>();
        var validUsername = configuration["BasicAuth:Username"];
        var validPassword = configuration["BasicAuth:Password"];

        if (string.IsNullOrEmpty(validUsername) || string.IsNullOrEmpty(validPassword) ||
            !string.Equals(username, validUsername, StringComparison.Ordinal) ||
            !string.Equals(password, validPassword, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}