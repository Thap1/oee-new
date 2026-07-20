namespace OeeNew.Application.Identity;

/// <summary>
/// Represents the central Identity Provider's one-time credential-issuance call (AD-7) a site makes
/// when a new user is created — distinct from role-scoping, which is written at the site (AD-4).
/// In this codebase's current single-instance topology (AppMode Site/Central separation is not yet
/// wired — see Story 1.1's deferred "AppMode isn't gated" finding), the central side is the same
/// process/database, so the Infrastructure implementation just hashes and returns; no network hop
/// exists yet. If/when true multi-instance deployment (Epic 5) separates Site from Central, this
/// becomes a real HTTP call, and <see cref="CredentialProvisioningException"/> becomes the actual
/// "central unreachable" case referenced by AC #2.
/// </summary>
public interface ICentralCredentialProvisioner
{
    /// <returns>The credential's password hash, as issued/stored by the central Identity Provider.</returns>
    /// <exception cref="CredentialProvisioningException">The central Identity Provider could not be reached.</exception>
    Task<string> ProvisionAsync(string username, string password, CancellationToken cancellationToken = default);
}
