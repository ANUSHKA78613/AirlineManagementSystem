using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.Json;

namespace Shared.Middleware
{
    /// <summary>
    /// Validates X-Api-Key header for service-to-service calls.
    /// Skips validation if the request already has a valid JWT Bearer token,
    /// or if the path is marked as public (health, swagger, auth endpoints).
    /// </summary>
    public class ApiKeyMiddleware
    {
        private const string ApiKeyHeaderName = "X-Api-Key";
        private readonly RequestDelegate _next;
        private readonly HashSet<string> _validApiKeys;
        private readonly bool _enabled;

        private static readonly HashSet<string> BypassPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/health",
            "/swagger",
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/forgot-password",
            "/api/auth/reset-password",
            "/api/auth/send-otp",
            "/api/auth/verify-otp",
            "/api/sso",
            "/api/flight" // Public flight, airport, route, and search queries
        };

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;

            var keysSection = configuration.GetSection("ApiKeys");
            var keys = keysSection.Get<string[]>();

            if (keys != null && keys.Length > 0)
            {
                _validApiKeys = new HashSet<string>(keys);
                _enabled = true;
            }
            else
            {
                _validApiKeys = new HashSet<string>();
                _enabled = false;
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // If no API keys configured, skip validation entirely
            if (!_enabled)
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Bypass for public/excluded paths
            if (BypassPaths.Any(bp => path.StartsWith(bp)))
            {
                await _next(context);
                return;
            }

            // Bypass if request already has a valid JWT Authorization header
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Validate API key
            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
                string.IsNullOrWhiteSpace(providedKey) ||
                !_validApiKeys.Contains(providedKey.ToString()))
            {
                Log.Warning("[ApiKey] Unauthorized request to {Path} — missing or invalid API key", path);
                //Send Error Response
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                //Create Error Body

                var body = JsonSerializer.Serialize(new
                {
                    error = "Unauthorized. Valid API key or Bearer token is required.",
                    status = 401
                });

                await context.Response.WriteAsync(body);
                return;
            }

            await _next(context);
        }
    }
}
