using Microsoft.AspNetCore.Builder;

namespace Shared.Middleware
{
    public static class MiddlewareExtensions
    {
        /// <summary>
        /// Registers the full shared middleware pipeline in the correct order.
        /// Call BEFORE UseAuthentication / UseAuthorization.
        ///
        /// Order:
        ///   1. PerformanceMiddleware     — times every request, adds X-Response-Time header
        ///   2. CorrelationIdMiddleware   — assigns/propagates X-Correlation-Id
        ///   3. RateLimitingMiddleware    — sliding-window per-IP rate limiter
        ///   4. RequestResponseLoggingMiddleware — structured req/res logging
        ///   5. ApiKeyMiddleware          — X-Api-Key validation for service-to-service
        ///   6. GlobalExceptionMiddleware — catches unhandled exceptions → JSON error response
        /// </summary>
        public static IApplicationBuilder UseSharedMiddleware(this IApplicationBuilder app)
        {
            app.UseMiddleware<PerformanceMiddleware>();
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<RateLimitingMiddleware>();
            app.UseMiddleware<RequestResponseLoggingMiddleware>();
            app.UseMiddleware<ApiKeyMiddleware>();
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseMiddleware<IdempotencyMiddleware>();
            return app;
        }
    }
}
