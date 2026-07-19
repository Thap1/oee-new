using System.Text.Json.Serialization;

namespace OeeNew.Infrastructure.Identity;

public sealed class JwkDto
{
    [JsonPropertyName("kty")] public string KeyType { get; init; } = "RSA";
    [JsonPropertyName("use")] public string Use { get; init; } = "sig";
    [JsonPropertyName("alg")] public string Algorithm { get; init; } = "RS256";
    [JsonPropertyName("kid")] public required string KeyId { get; init; }
    [JsonPropertyName("n")] public required string Modulus { get; init; }
    [JsonPropertyName("e")] public required string Exponent { get; init; }
}

public sealed class JwksDocument
{
    [JsonPropertyName("keys")] public required IReadOnlyList<JwkDto> Keys { get; init; }
}
