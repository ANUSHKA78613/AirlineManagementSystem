using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;

namespace Shared.Middleware
{
    /// <summary>
    /// Idempotency middleware that prevents duplicate POST/PUT/PATCH requests
    /// within a configurable time window using Redis cache.
    /// Clients must send 'X-Idempotency-Key' header for state-changing operations.
    /// </summary>
    public class IdempotencyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDistributedCache _cache;
        private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromMinutes(10);

        public IdempotencyMiddleware(RequestDelegate next, IDistributedCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only applies to state-changing methods
            var method = context.Request.Method.ToUpper();
            if (method != "POST" && method != "PUT" && method != "PATCH")
            {
                await _next(context);
                return;
            }

            var idempotencyKey = context.Request.Headers["X-Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                // No key provided — proceed normally (backwards compatible)
                await _next(context);
                return;
            }

            var cacheKey = $"idempotency:{idempotencyKey}";
            var cached = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cached))
            {
                Log.Warning("Idempotent request blocked — duplicate key: {Key}", idempotencyKey);

                context.Response.StatusCode = 409;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    $"{{\"error\":\"Duplicate request detected\",\"idempotencyKey\":\"{idempotencyKey}\",\"cachedResponse\":{cached}}}");
                return;
            }

            // Capture response body
            var originalBody = context.Response.Body;
            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            await _next(context);

            memoryStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
            memoryStream.Seek(0, SeekOrigin.Begin);

            // Only cache successful responses (2xx)
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                await _cache.SetStringAsync(cacheKey, responseBody, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = IdempotencyWindow
                });
            }

            await memoryStream.CopyToAsync(originalBody);
            context.Response.Body = originalBody;
        }
    }
}
