using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using OeeNew.Api.Errors;
using OeeNew.Infrastructure.Sync;

namespace OeeNew.Api.Sync;

/// <summary>
/// Guards the machine-to-machine sync endpoint (Story 5.1) with a single shared secret instead of the
/// user-facing JWT Bearer scheme — a Site's push loop is a headless background service with no
/// interactive login and no natural "user," so reusing the human Identity Provider would mean minting a
/// fake machine <c>User</c> row for no real benefit. Constant-time comparison avoids a timing side-channel
/// on the key check.
/// </summary>
public sealed class ApiKeyAuthFilter(IOptions<SyncOptions> options) : IAsyncActionFilter
{
    private const string HeaderName = "X-Sync-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expectedKey = options.Value.ApiKey ?? string.Empty;
        var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(expectedKey) || !FixedTimeEquals(expectedKey, providedKey))
        {
            context.Result = new ObjectResult(new ApiErrorResponse
            {
                Code = "UNAUTHORIZED",
                Message = "A valid sync API key is required.",
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
            return;
        }

        await next();
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
