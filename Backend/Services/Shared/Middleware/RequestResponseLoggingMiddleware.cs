using Microsoft.AspNetCore.Http;
using Serilog;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Middleware
{
    /// <summary>
    /// Logs every HTTP request and response with structured properties.
    /// Sensitive fields (passwords, tokens) are masked.
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/health", "/swagger", "/favicon.ico"
        };

        private static readonly Regex SensitiveFieldRegex = new(
            @"(""(?:password|passwordHash|token|secret|apiKey|authorization|currentPassword|newPassword)""\s*:\s*)""[^""]*""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public RequestResponseLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Skip excluded paths
            if (ExcludedPaths.Any(ep => path.StartsWith(ep)))
            {
                await _next(context);
                return;
            }

            var sw = Stopwatch.StartNew();
            var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? "-";
            var method = context.Request.Method;
            var queryString = context.Request.QueryString.Value ?? "";

            // Read and log request body (for POST/PUT/PATCH only)
            string requestBody = string.Empty;
            if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method))
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                // Mask sensitive fields
                requestBody = MaskSensitiveData(requestBody);
                if (requestBody.Length > 4096) requestBody = requestBody[..4096] + "...(truncated)";
            }

            Log.Information(
                "[REQ] {Method} {Path}{Query} | CorrelationId={CorrelationId} | Body={RequestBody}",
                method, path, queryString, correlationId, requestBody);

            // Capture response
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();

                responseBody.Seek(0, SeekOrigin.Begin);
                var responseText = await new StreamReader(responseBody).ReadToEndAsync();
                responseBody.Seek(0, SeekOrigin.Begin);

                var truncatedResponse = responseText.Length > 4096
                    ? responseText[..4096] + "...(truncated)"
                    : responseText;

                truncatedResponse = MaskSensitiveData(truncatedResponse);

                Log.Information(
                    "[RES] {Method} {Path} → {StatusCode} | {ElapsedMs}ms | CorrelationId={CorrelationId} | Body={ResponseBody}",
                    method, path, context.Response.StatusCode, sw.ElapsedMilliseconds, correlationId, truncatedResponse);

                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private static string MaskSensitiveData(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return body;
            return SensitiveFieldRegex.Replace(body, "$1\"***MASKED***\"");
        }
    }
}
