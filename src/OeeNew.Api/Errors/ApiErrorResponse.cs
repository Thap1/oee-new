using System.Text.Json.Serialization;

namespace OeeNew.Api.Errors;

/// <summary>Standard API error envelope (Architecture Spine — Consistency Conventions).</summary>
public sealed class ApiErrorResponse
{
    [JsonPropertyName("code")] public required string Code { get; init; }
    [JsonPropertyName("message")] public required string Message { get; init; }
    [JsonPropertyName("details")] public object? Details { get; init; }
}
