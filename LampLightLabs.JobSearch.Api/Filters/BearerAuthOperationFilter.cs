using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LampLightLabs.JobSearch.Api.Filters;

/// <summary>
/// Swagger operation filter that adds a Bearer authentication security requirement
/// to any endpoint decorated with [Authorize] using the default JWT Bearer scheme.
/// This ensures the Swagger UI displays the padlock icon and sends the Authorization
/// header for Bearer-authenticated endpoints.
/// </summary>
public class BearerAuthOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasBearer = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any(a => string.IsNullOrEmpty(a.AuthenticationSchemes) ||
                      a.AuthenticationSchemes == "Bearer");

        if (!hasBearer) return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}