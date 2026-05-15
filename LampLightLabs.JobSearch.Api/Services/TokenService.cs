using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LampLightLabs.JobSearch.Api.Services;

/// <summary>
/// Generates signed JWT bearer tokens.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of <see cref="TokenService"/>.
    /// </summary>
    /// <param name="configuration">Application configuration for JWT settings.</param>
    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public string GenerateToken(string username)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key is not configured. Use user secrets or environment variables in production.");

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
