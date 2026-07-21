using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Auth;
using OeeNew.Application.Identity;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;

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
        MasterDataNotFoundException notFound => (StatusCodes.Status404NotFound,
            new ApiErrorResponse { Code = "NOT_FOUND", Message = notFound.Message }),
        MasterDataParentNotFoundException parentNotFound => (StatusCodes.Status400BadRequest,
            new ApiErrorResponse { Code = "PARENT_NOT_FOUND", Message = parentNotFound.Message }),
        MasterDataForbiddenException forbidden => (StatusCodes.Status403Forbidden,
            new ApiErrorResponse { Code = "FORBIDDEN", Message = forbidden.Message }),
        MasterDataHasDependentsException hasDependents => (StatusCodes.Status409Conflict,
            new ApiErrorResponse
            {
                Code = "HAS_DEPENDENTS",
                Message = hasDependents.Message,
                Details = new { dependentNames = hasDependents.DependentNames },
            }),
        MasterDataValidationException validation => (StatusCodes.Status400BadRequest,
            new ApiErrorResponse { Code = "VALIDATION_ERROR", Message = validation.Message }),
        DowntimeEventNotOpenException notOpen => (StatusCodes.Status404NotFound,
            new ApiErrorResponse { Code = "DOWNTIME_EVENT_NOT_OPEN", Message = notOpen.Message }),
        ShiftOverlapException overlap => (StatusCodes.Status409Conflict,
            new ApiErrorResponse { Code = "SHIFT_OVERLAP", Message = overlap.Message }),
        UsernameAlreadyTakenException taken => (StatusCodes.Status409Conflict,
            new ApiErrorResponse { Code = "USERNAME_TAKEN", Message = taken.Message }),
        CredentialProvisioningException provisioning => (StatusCodes.Status503ServiceUnavailable,
            new ApiErrorResponse { Code = "CREDENTIAL_PROVISIONING_FAILED", Message = provisioning.Message }),
        DbUpdateException => (StatusCodes.Status409Conflict,
            new ApiErrorResponse { Code = "CONFLICT", Message = "The operation could not be completed because a related record changed concurrently. Please retry." }),
        _ => (StatusCodes.Status500InternalServerError,
            new ApiErrorResponse { Code = "INTERNAL_ERROR", Message = "An unexpected error occurred." }),
    };
}
