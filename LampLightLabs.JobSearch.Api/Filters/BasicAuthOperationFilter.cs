using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LampLightLabs.JobSearch.Api.Filters;

/// <summary>
/// Swagger operation filter that adds a Basic authentication security requirement
/// to any endpoint decorated with [Authorize(AuthenticationSchemes = "Basic")].
/// This ensures the Swagger UI displays the padlock icon and credential input
/// for Basic-authenticated endpoints.
/// </summary>
public class BasicAuthOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasBasicAuth = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any(a => a.AuthenticationSchemes == "Basic");

        if (!hasBasicAuth) return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Basic"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}
