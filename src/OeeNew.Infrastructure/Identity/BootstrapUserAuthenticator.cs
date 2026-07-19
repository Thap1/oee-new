using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OeeNew.Application.Auth;

namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Validates credentials against a single configured Admin account (see BootstrapAdminOptions).
/// Admin's SiteIds/LineIds are always empty — the `role` claim is global per AD-7, it does not
/// carry a site/line scope. Story 1.4 introduces the persisted User entity + role-scoping and
/// supersedes this class with a real multi-user IUserAuthenticator; nothing else in the codebase
/// depends on this class directly (only on IUserAuthenticator), so that swap is a DI registration
/// change, not a rewrite.
/// </summary>
public sealed class BootstrapUserAuthenticator(IOptions<BootstrapAdminOptions> options) : IUserAuthenticator
{
    private readonly IPasswordHasher<BootstrapUserAuthenticator> _hasher = new PasswordHasher<BootstrapUserAuthenticator>();

    public Task<AuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var admin = options.Value;

        if (!string.Equals(username, admin.Username, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(admin.PasswordHash))
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        var verification = _hasher.VerifyHashedPassword(this, admin.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        var result = new AuthenticatedUser(admin.UserId, admin.Username, "Admin", SiteIds: [], LineIds: []);
        return Task.FromResult<AuthenticatedUser?>(result);
    }
}
