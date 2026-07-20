using OeeNew.Application.Auth;

namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Tries the persisted multi-user store first (Story 1.4); falls back to the single config-driven
/// bootstrap Admin (Story 1.1) when the username isn't found there, or when the persisted store
/// can't be reached at all (e.g. no database configured yet). This fallback is what keeps the very
/// first Admin login working before any User row exists — the reason
/// <see cref="BootstrapUserAuthenticator"/>/<c>BootstrapAdminOptions</c> are kept rather than deleted.
/// </summary>
public sealed class CompositeUserAuthenticator(PersistedUserAuthenticator persisted, BootstrapUserAuthenticator bootstrap) : IUserAuthenticator
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Persisted store unreachable/unconfigured — fall through to the bootstrap Admin.
        }

        return await bootstrap.ValidateCredentialsAsync(username, password, cancellationToken);
    }
}
