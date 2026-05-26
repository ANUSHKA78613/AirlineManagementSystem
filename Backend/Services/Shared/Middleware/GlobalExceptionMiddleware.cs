using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Serilog;
using Shared.Middleware.Exceptions;

namespace Shared.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public GlobalExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            // ── Custom App Exceptions (priority) ──────────────────────
            catch (AppValidationException ex)
            {
                context.Response.StatusCode = ex.StatusCode;
                context.Response.ContentType = "application/json"; //the response I'm sending back is in JSON format."

                var body = new
                {
                    error = ex.Message,
                    errors = ex.Errors,
                    traceId = context.TraceIdentifier,
                    status = ex.StatusCode
                };

                Log.Warning(ex, "Validation exception | TraceId={TraceId} | Path={Path}",
                    context.TraceIdentifier, context.Request.Path);

                await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
            }
            catch (AppException ex)
            {
                Log.Warning(ex, "Domain exception [{StatusCode}] | TraceId={TraceId} | Path={Path}",
                    ex.StatusCode, context.TraceIdentifier, context.Request.Path);

                await WriteErrorAsync(context, (HttpStatusCode)ex.StatusCode, ex.Message, ex);
            }
            // ── FluentValidation ──────────────────────────────────────
            catch (FluentValidation.ValidationException ex)
            {
                var errors = ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray());

                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";

                var body = new
                {
                    error = "Validation failed.",
                    errors,
                    traceId = context.TraceIdentifier,
                    status = 400
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
            }
            // ── Legacy System Exceptions (backward compat) ────────────
            catch (UnauthorizedAccessException ex)
            {
                await WriteErrorAsync(context, HttpStatusCode.Forbidden, "Access denied.", ex);
            }
            catch (KeyNotFoundException ex)
            {
                await WriteErrorAsync(context, HttpStatusCode.NotFound, ex.Message, ex);
            }
            catch (ArgumentException ex)
            {
                await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message, ex);
            }
            catch (InvalidOperationException ex)
            {
                await WriteErrorAsync(context, HttpStatusCode.Conflict, ex.Message, ex);
            }
            // ── Catch-All ─────────────────────────────────────────────
            catch (Exception ex)
            {
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError,
                    "An unexpected error occurred.", ex);
            }
        }

        private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode,
            string message, Exception ex)
        {
            var traceId = context.TraceIdentifier;

            Log.Error(ex, "Unhandled exception | TraceId={TraceId} | Path={Path} | Method={Method}",
                traceId, context.Request.Path, context.Request.Method);

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var body = new
            {
                error = message,
                traceId,
                status = (int)statusCode
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
    }
}
