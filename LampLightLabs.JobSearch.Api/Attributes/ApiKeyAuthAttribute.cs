using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace LampLightLabs.JobSearch.Api.Attributes;

/// <summary>
/// Enforces API key authentication for the decorated controller or action.
/// The client must supply the key in the Authorization header using the ApiKey scheme.
/// </summary>
/// <remarks>
/// Expected header format: Authorization: ApiKey {key}
///
/// The valid key is read from configuration at ApiKey:Key.
/// Use user secrets locally and environment variables in production —
/// never commit a real key to source control.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAuthAttribute : Attribute, IAuthorizationFilter
{
    private const string ApiKeyScheme = "ApiKey";

    /// <inheritdoc />
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();

        // Expect format: "ApiKey <key>"
        if (!authHeader.StartsWith($"{ApiKeyScheme} ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var providedKey = authHeader[$"{ApiKeyScheme} ".Length..].Trim();
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var validKey = configuration["ApiKey:Key"];

        if (string.IsNullOrEmpty(validKey) || !string.Equals(providedKey, validKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Valid key - set an authenticated identity so downstream filters see an authenticated user.
        context.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, "ApiKeyClient") },
                ApiKeyScheme));
    }
}
