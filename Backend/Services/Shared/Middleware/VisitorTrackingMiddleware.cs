using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Serilog;
using System.Text.Json;

namespace Shared.Middleware
{
    public class VisitorTrackingMiddleware
    {
        private readonly RequestDelegate _next;

        public VisitorTrackingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
        {
            try
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                if (ipAddress != "Unknown")
                {
                    string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    string cacheKey = $"visitors:{today}:{ipAddress}";
                    
                    var visited = await cache.GetStringAsync(cacheKey);
                    if (string.IsNullOrEmpty(visited))
                    {
                        // Set the IP as visited today with a 24-hour expiration
                        var options = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                        };
                        await cache.SetStringAsync(cacheKey, "true", options);

                        // Increment the global daily unique counter
                        string counterKey = $"visitors_count:{today}";
                        var counterStr = await cache.GetStringAsync(counterKey);
                        int count = string.IsNullOrEmpty(counterStr) ? 0 : int.Parse(counterStr);
                        count++;
                        
                        await cache.SetStringAsync(counterKey, count.ToString(), options);

                        Log.Information("Tracking unique visitor: {IP} for {Date}", ipAddress, today);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never block the request if cache fails
                Log.Warning(ex, "Failed to track visitor IP in Redis.");
            }

            await _next(context);
        }
    }
}
