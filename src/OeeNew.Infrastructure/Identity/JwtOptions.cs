namespace OeeNew.Infrastructure.Identity;

/// <summary>Bound from configuration section "Jwt".</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "oee-new-central";
    public string Audience { get; set; } = "oee-new-api";
    public int AccessTokenLifetimeMinutes { get; set; } = 480;
}
