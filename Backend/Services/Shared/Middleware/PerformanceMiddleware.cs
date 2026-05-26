using Microsoft.AspNetCore.Http;
using Serilog;
using System.Diagnostics;

namespace Shared.Middleware
{
    /// <summary>
    /// Measures request processing duration and adds X-Response-Time header.
    /// Logs slow requests (> 500ms) as Warning, very slow (> 2s) as Error.
    /// </summary>
    public class PerformanceMiddleware
    {
        private readonly RequestDelegate _next;
        private const int SlowThresholdMs = 500;
        private const int VerySlowThresholdMs = 2000;

        public PerformanceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();

            context.Response.OnStarting(() =>
            {
                sw.Stop();
                context.Response.Headers["X-Response-Time"] = $"{sw.ElapsedMilliseconds}ms";
                return Task.CompletedTask;
            });

            try
            {
                await _next(context);
            }
            finally
            {
                if (sw.IsRunning) sw.Stop();

                var elapsed = sw.ElapsedMilliseconds;
                var method = context.Request.Method;
                var path = context.Request.Path.Value ?? "/";

                if (elapsed >= VerySlowThresholdMs)
                {
                    Log.Error(
                        "[PERF] VERY SLOW request: {Method} {Path} took {ElapsedMs}ms (threshold: {Threshold}ms)",
                        method, path, elapsed, VerySlowThresholdMs);
                }
                else if (elapsed >= SlowThresholdMs)
                {
                    Log.Warning(
                        "[PERF] Slow request: {Method} {Path} took {ElapsedMs}ms (threshold: {Threshold}ms)",
                        method, path, elapsed, SlowThresholdMs);
                }
            }
        }
    }
}
