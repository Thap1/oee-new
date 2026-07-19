using Microsoft.AspNetCore.Diagnostics;
using OeeNew.Application.Auth;

namespace OeeNew.Api.Errors;

/// <summary>
/// Maps every unhandled exception to the standard error envelope `{ code, message, details? }`,
/// never leaking raw .NET exception details (Architecture Spine — Consistency Conventions).
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, error) = Map(exception);

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }

    private static (int StatusCode, ApiErrorResponse Error) Map(Exception exception) => exception switch
    {
        InvalidCredentialsException => (StatusCodes.Status401Unauthorized,
            new ApiErrorResponse { Code = "INVALID_CREDENTIALS", Message = "Invalid username or password." }),
        _ => (StatusCodes.Status500InternalServerError,
            new ApiErrorResponse { Code = "INTERNAL_ERROR", Message = "An unexpected error occurred." }),
    };
}
