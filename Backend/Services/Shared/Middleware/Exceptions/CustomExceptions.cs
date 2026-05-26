namespace Shared.Middleware.Exceptions
{
    // Base class for all domain/custom exceptions.
    // GlobalExceptionMiddleware catches these and maps StatusCode → HTTP response.
    public abstract class AppException : Exception
    {
        public int StatusCode { get; }
        protected AppException(string message, int statusCode = 500) : base(message) => StatusCode = statusCode;
        protected AppException(string message, int statusCode, Exception inner) : base(message, inner) => StatusCode = statusCode;
    }

    // 404 — Resource not found.Resource doesn't exist in database
    public class NotFoundException : AppException
    {
        public NotFoundException(string resource, object key)
            : base($"{resource} with identifier '{key}' was not found.", 404) { }
        public NotFoundException(string message) : base(message, 404) { }
    }

    // 400 — Bad request / invalid input. Client sends invalid/malformed input
    public class BadRequestException : AppException
    {
        public BadRequestException(string message) : base(message, 400) { }
    }

    // 409 — Conflict / duplicate resource.Duplicate resource or uniqueness constraint violated
    public class ConflictException : AppException
    {
        public ConflictException(string message) : base(message, 409) { }
    }

    // 401 — Authentication failure.
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message = "Invalid credentials.") : base(message, 401) { }
    }

    // 403 — Authorization failure.
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message = "You do not have permission to perform this action.") : base(message, 403) { }
    }

    // 400 — Validation failure with field-level errors.
    public class AppValidationException : AppException
    {
        public IDictionary<string, string[]> Errors { get; }
        public AppValidationException(IDictionary<string, string[]> errors)
            : base("One or more validation errors occurred.", 400)
        {
            Errors = errors;
        }
        public AppValidationException(string field, string error)
            : base($"Validation failed for '{field}': {error}", 400)
        {
            Errors = new Dictionary<string, string[]> { { field, new[] { error } } };
        }
    }

    // 422 — Business rule violation.Business logic rule violated
    public class DomainException : AppException
    {
        public DomainException(string message) : base(message, 422) { }
    }

    // 503 — Downstream service unavailable.
    public class ServiceUnavailableException : AppException
    {
        public ServiceUnavailableException(string service)
            : base($"The '{service}' service is currently unavailable. Please try again later.", 503) { }
    }
}
