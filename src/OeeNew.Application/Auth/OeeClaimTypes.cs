namespace OeeNew.Application.Auth;

/// <summary>
/// Custom JWT claim type names (AD-7). `Role` is a single value; `Admin` is global (no site/line
/// claims). `SiteId`/`LineId` are repeated claims — one entry per Site/Line the user is scoped to.
/// </summary>
public static class OeeClaimTypes
{
    public const string Role = "role";
    public const string SiteId = "site_id";
    public const string LineId = "line_id";
}
