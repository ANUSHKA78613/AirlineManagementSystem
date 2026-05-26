using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace Shared.Middleware
{
    /// <summary>
    /// Sliding-window rate limiter per client IP.
    /// Returns 429 Too Many Requests when the limit is exceeded.
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly int _maxRequests;
        private readonly TimeSpan _window;

        // key = IP, value = sorted list of request timestamps
        private static readonly ConcurrentDictionary<string, SlidingWindow> _clients = new();
        private static DateTime _lastCleanup = DateTime.UtcNow;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

        public RateLimitingMiddleware(RequestDelegate next, int maxRequests = 100, int windowSeconds = 60)
        {
            _next = next;
            _maxRequests = maxRequests;
            _window = TimeSpan.FromSeconds(windowSeconds);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip rate limiting for health checks and swagger
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            if (path.Contains("/health") || path.Contains("/swagger"))
            {
                await _next(context);
                return;
            }

            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            // Periodic cleanup of stale entries
            if (now - _lastCleanup > CleanupInterval)
            {
                _lastCleanup = now;
                CleanupStaleEntries(now);
            }

            var window = _clients.GetOrAdd(clientIp, _ => new SlidingWindow());

            lock (window)
            {
                // Remove timestamps outside the sliding window
                window.Timestamps.RemoveAll(t => now - t > _window);

                if (window.Timestamps.Count >= _maxRequests)
                {
                    var oldestInWindow = window.Timestamps.Min();
                    var retryAfter = (int)Math.Ceiling((_window - (now - oldestInWindow)).TotalSeconds);
                    retryAfter = Math.Max(retryAfter, 1);

                    context.Response.StatusCode = 429;
                    context.Response.Headers["Retry-After"] = retryAfter.ToString();
                    context.Response.ContentType = "application/json";

                    var body = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "Too many requests. Please try again later.",
                        retryAfterSeconds = retryAfter,
                        status = 429
                    });

                    context.Response.WriteAsync(body).GetAwaiter().GetResult();
                    return;
                }

                window.Timestamps.Add(now);
            }

            await _next(context);
        }

        private static void CleanupStaleEntries(DateTime now)
        {
            foreach (var kvp in _clients)
            {
                lock (kvp.Value)
                {
                    kvp.Value.Timestamps.RemoveAll(t => now - t > TimeSpan.FromMinutes(2));
                    if (kvp.Value.Timestamps.Count == 0)
                    {
                        _clients.TryRemove(kvp.Key, out _);
                    }
                }
            }
        }

        private class SlidingWindow
        {
            public List<DateTime> Timestamps { get; } = new();
        }
    }
}
