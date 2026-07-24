using OeeNew.Application.Auth;
using OeeNew.Application.Identity;

namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Tries the persisted multi-user store first (Story 1.4); falls back to the single config-driven
/// bootstrap Admin (Story 1.1) only when no persisted user exists for that username, or when the
/// persisted store can't be reached at all (e.g. no database configured yet). This fallback is what
/// keeps the very first Admin login working before any User row exists — the reason
/// <see cref="BootstrapUserAuthenticator"/>/<c>BootstrapAdminOptions</c> are kept rather than deleted.
/// A persisted user that exists but was given the wrong password must NOT fall through — otherwise
/// the bootstrap password would silently work as a permanent alternate credential for any persisted
/// username that happens to collide with the configured bootstrap username (Story 1.4 review).
/// </summary>
public sealed class CompositeUserAuthenticator(PersistedUserAuthenticator persisted, BootstrapUserAuthenticator bootstrap, IUserRepository users) : IUserAuthenticator
{
    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await persisted.ValidateCredentialsAsync(username, password, cancellationToken);
            if (user is not null)
            {
                return user;
            }

            if (await users.GetByUsernameAsync(username, cancellationToken) is not null)
            {
                // A persisted user with this username exists but the password didn't match it —
                // do not let the bootstrap Admin's password serve as a second valid credential.
                return null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Persisted store unreachable/unconfigured — fall through to the bootstrap Admin.
        }

        return await bootstrap.ValidateCredentialsAsync(username, password, cancellationToken);
    }
}
