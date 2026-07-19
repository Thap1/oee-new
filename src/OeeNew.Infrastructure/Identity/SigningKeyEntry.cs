using System.Security.Cryptography;

namespace OeeNew.Infrastructure.Identity;

/// <summary>One RSA keypair identified by a `kid`, held by <see cref="IJwtSigningKeyProvider"/>.</summary>
public sealed class SigningKeyEntry(string keyId, RSA rsa)
{
    public string KeyId { get; } = keyId;
    public RSA Rsa { get; } = rsa;
}
