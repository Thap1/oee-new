using Microsoft.AspNetCore.Identity;
using OeeNew.Application.Auth;
using OeeNew.Application.Identity;
using OeeNew.Domain.Identity;

namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Validates credentials against the persisted multi-user store (Story 1.4). Supersedes
/// <see cref="BootstrapUserAuthenticator"/> as the primary authentication path — see
/// <see cref="CompositeUserAuthenticator"/> for why bootstrap is kept as a fallback rather than deleted.
/// </summary>
public sealed class PersistedUserAuthenticator(IUserRepository users) : IUserAuthenticator
{
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.UpdatePasswordHash(_hasher.HashPassword(user, password));
            await users.UpdateAsync(user, cancellationToken);
        }

        return new AuthenticatedUser(user.Id, user.Username, user.Role.ToString(), user.SiteIds, user.LineIds);
    }
}
