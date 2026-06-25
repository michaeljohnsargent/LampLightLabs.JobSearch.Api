using Asp.Versioning;
using LampLightLabs.JobSearch.Api.Models.Auth;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LampLightLabs.JobSearch.Api.Controllers.V1;

/// <summary>
/// Handles authentication and JWT token generation.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{v:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthController"/>.
    /// </summary>
    /// <param name="tokenService">Service for generating JWT tokens.</param>
    public AuthController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    /// <summary>
    /// Authenticates a user and returns a signed JWT bearer token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>A JWT token and expiry timestamp.</returns>
    /// <remarks>
    /// Demo credentials: username = "demo", password = "password"
    ///
    /// In production, validate credentials against a real user store
    /// such as ASP.NET Core Identity or a database.
    ///
    /// Sample request:
    ///
    ///     POST /api/v1/auth/token
    ///     {
    ///         "username": "demo",
    ///         "password": "password"
    ///     }
    ///
    /// </remarks>
    [HttpPost("token")]
    [EnableRateLimiting("auth-fixed")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetToken([FromBody] LoginRequest request)
    {
        // Demo: hardcoded credentials for practice purposes.
        // Replace with Identity, database lookup, or external provider in production.
        if (request.Username != "demo" || request.Password != "password")
        {
            return Unauthorized();
        }

        var token = _tokenService.GenerateToken(request.Username);

        return Ok(new TokenResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }
}
