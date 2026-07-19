namespace OeeNew.Application.Auth;

/// <summary>
/// Abstraction over the central Identity Provider's credential store (AD-7).
/// Application depends only on this interface; Infrastructure supplies the implementation
/// (EF Core + Postgres user store, once introduced by Story 1.4 — see BootstrapUserAuthenticator
/// for the seam this story uses while no persisted User entity exists yet).
/// </summary>
public interface IUserAuthenticator
{
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
}
