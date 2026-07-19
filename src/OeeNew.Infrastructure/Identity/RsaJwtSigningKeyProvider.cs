using System.Security.Cryptography;
using System.Threading;

namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// In-process RSA key store for the central Identity Provider (AD-7).
/// Thread-safe; keeps at most 2 keys (current + previous) as required by AD-7's
/// "minimum 2 cached signing keys" rule so a rotation never invalidates in-flight tokens.
/// </summary>
public sealed class RsaJwtSigningKeyProvider : IJwtSigningKeyProvider, IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<SigningKeyEntry> _retired = [];
    private SigningKeyEntry _current;
    private SigningKeyEntry? _previous;

    public RsaJwtSigningKeyProvider()
    {
        _current = CreateKey();
    }

    public SigningKeyEntry GetSigningKey()
    {
        lock (_lock)
        {
            return _current;
        }
    }

    public IReadOnlyList<SigningKeyEntry> GetValidationKeys()
    {
        lock (_lock)
        {
            return _previous is null ? [_current] : [_current, _previous];
        }
    }

    public void RotateKey()
    {
        lock (_lock)
        {
            // Don't dispose the outgoing "previous" key here: a concurrent request may still be
            // mid-validation against it (GetValidationKeys() hands out the live RSA instance, and
            // the actual signature check happens outside this lock, in the JWT bearer pipeline).
            // Retire it instead and dispose only when the provider itself is torn down.
            if (_previous is not null)
            {
                _retired.Add(_previous);
            }

            _previous = _current;
            _current = CreateKey();
        }
    }

    private static SigningKeyEntry CreateKey() => new(Guid.NewGuid().ToString("N"), RSA.Create(2048));

    public void Dispose()
    {
        lock (_lock)
        {
            _current.Rsa.Dispose();
            _previous?.Rsa.Dispose();
            foreach (var retired in _retired)
            {
                retired.Rsa.Dispose();
            }
            _retired.Clear();
        }
    }
}
