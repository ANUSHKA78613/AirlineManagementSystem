using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Identity.Application.CQRS.Commands;

namespace Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SsoController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;

        public SsoController(IMediator mediator, IConfiguration configuration)
        {
            _mediator = mediator;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpGet("providers")]
        public IActionResult GetProviders()
        {
            return Ok(new[]
            {
                new { provider = "Google", endpoint = "/api/sso/google", icon = "google" }
            });
        }

        [AllowAnonymous]
        [HttpGet("google")]
        public async Task<IActionResult> GoogleLogin([FromQuery] string? returnUrl = null)
        {
            var clientId = _configuration["Google:ClientId"];
            if (string.IsNullOrEmpty(clientId) || clientId.Contains("placeholder"))
            {
                var token = await _mediator.Send(new ProcessSsoLoginCommand("google.demo.user@gmail.com", "mock-google-123456789", "Google Demo User", "https://img.icons8.com/color/48/000000/google-logo.png"));
                var frontendUrl = returnUrl ?? "http://localhost:4200";
                if (!frontendUrl.StartsWith("http")) frontendUrl = $"http://localhost:4200{frontendUrl}";
                var separator = frontendUrl.Contains('?') ? '&' : '?';
                return Redirect($"{frontendUrl}{separator}sso_token={token}&sso_provider=Google");
            }

            var redirectUrl = Url.Action(nameof(GoogleCallback), "Sso", new { returnUrl });
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl,
                Items = { { "returnUrl", returnUrl ?? "/" } }
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [AllowAnonymous]
        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = null)
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
            {
                return BadRequest(new { error = "Google authentication failed.", details = authenticateResult.Failure?.Message });
            }

            var claims = authenticateResult.Principal?.Claims;
            var googleId = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value;
            var picture = claims?.FirstOrDefault(c => c.Type == "urn:google:picture")?.Value
                          ?? claims?.FirstOrDefault(c => c.Type == "picture")?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
            {
                return BadRequest(new { error = "Could not retrieve email or ID from Google." });
            }

            var token = await _mediator.Send(new ProcessSsoLoginCommand(email, googleId, name, picture));

            var frontendUrl = returnUrl ?? "http://localhost:4200";
            if (!frontendUrl.StartsWith("http")) frontendUrl = $"http://localhost:4200{frontendUrl}";

            var separator = frontendUrl.Contains('?') ? '&' : '?';
            return Redirect($"{frontendUrl}{separator}sso_token={token}&sso_provider=Google");
        }
    }
}
