namespace OeeNew.Application.Auth;

/// <summary>
/// Result of successfully validating credentials against the central Identity Provider (AD-7).
/// SiteIds/LineIds are empty for a global Admin (AD-7: role claim is global, not site-scoped).
/// </summary>
public sealed record AuthenticatedUser(
    Guid UserId,
    string Username,
    string Role,
    IReadOnlyList<Guid> SiteIds,
    IReadOnlyList<Guid> LineIds);
