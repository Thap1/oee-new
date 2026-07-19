namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Bound from configuration section "BootstrapAdmin". Holds the single Admin credential needed
/// to log in before any user has been created via Story 1.4's user management. Story 1.4 replaces
/// <see cref="BootstrapUserAuthenticator"/> with a persisted, multi-user implementation of
/// IUserAuthenticator — this options class and its authenticator are a deliberately narrow seam,
/// not a placeholder for a wider user schema.
/// </summary>
public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public Guid UserId { get; set; } = Guid.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by ASP.NET Core's <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>.</summary>
    public string PasswordHash { get; set; } = string.Empty;
}
