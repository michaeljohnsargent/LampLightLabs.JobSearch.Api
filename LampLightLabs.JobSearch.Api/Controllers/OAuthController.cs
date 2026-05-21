using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    [Route("oauth")]
    [ApiController]
    public class OAuthController : ControllerBase
    {
        private readonly IOAuthClientService _clientService;
        private readonly ITokenService _tokenService;

        public OAuthController(IOAuthClientService clientService, ITokenService tokenService)
        {
            _clientService = clientService;
            _tokenService = tokenService;
        }

        [HttpPost("token")]
        public IActionResult GetToken([FromForm] Models.Auth.OAuthTokenRequest request)
        {
            if (request.GrantType != "client_credentials")
                return BadRequest("unsupported_grant_type");

            if (!_clientService.ValidateClient(request.ClientId, request.ClientSecret))
                return Unauthorized("invalid_client");

            var token = _tokenService.GenerateClientToken(request.ClientId, request.Scope);

            return Ok(new Models.Auth.OAuthTokenResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 3600,
                Scope = request.Scope
            });
        }

    }
}
