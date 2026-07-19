namespace OeeNew.Api.Errors;

/// <summary>Writes the standard error envelope directly to the response (used where the exception
/// handler pipeline is bypassed, e.g. authentication challenges).</summary>
public static class ApiErrorWriter
{
    public static Task WriteAsync(HttpContext context, int statusCode, string code, string message, object? details = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse { Code = code, Message = message, Details = details });
    }
}
