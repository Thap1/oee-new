namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Holds the central Identity Provider's signing keys (AD-7): the current key used to sign new
/// tokens, plus at least the immediately previous key so tokens signed before a rotation remain
/// valid until their own expiry ("overlap window").
/// </summary>
public interface IJwtSigningKeyProvider
{
    /// <summary>Current key used to sign newly issued tokens.</summary>
    SigningKeyEntry GetSigningKey();

    /// <summary>All keys accepted for validating incoming tokens (current + previous).</summary>
    IReadOnlyList<SigningKeyEntry> GetValidationKeys();

    /// <summary>
    /// Rotates the signing key: current becomes previous, a new key becomes current.
    /// Only the current + immediately previous key are retained (AD-7 minimum cache of 2).
    /// </summary>
    void RotateKey();
}
