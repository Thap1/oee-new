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
    // Long enough to outlast any in-flight validation racing a rotation; short enough that
    // repeated rotation doesn't accumulate unbounded RSA handles (see RotateKey/DisposeRetiredKey).
    private static readonly TimeSpan RetiredKeyGracePeriod = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private readonly List<SigningKeyEntry> _retired = [];
    private readonly List<Timer> _retiredKeyTimers = [];
    private SigningKeyEntry _current;
    private SigningKeyEntry? _previous;
    private bool _disposed;

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
            // Retire it and dispose after a grace period instead — not only at provider shutdown,
            // since a long-running process may rotate many times over its lifetime.
            if (_previous is not null)
            {
                var retiring = _previous;
                _retired.Add(retiring);
                _retiredKeyTimers.Add(new Timer(_ => DisposeRetiredKey(retiring), null, RetiredKeyGracePeriod, Timeout.InfiniteTimeSpan));
            }

            _previous = _current;
            _current = CreateKey();
        }
    }

    private void DisposeRetiredKey(SigningKeyEntry entry)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (_retired.Remove(entry))
            {
                entry.Rsa.Dispose();
            }
        }
    }

    private static SigningKeyEntry CreateKey() => new(Guid.NewGuid().ToString("N"), RSA.Create(2048));

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;

            _current.Rsa.Dispose();
            _previous?.Rsa.Dispose();
            foreach (var retired in _retired)
            {
                retired.Rsa.Dispose();
            }
            _retired.Clear();

            foreach (var timer in _retiredKeyTimers)
            {
                timer.Dispose();
            }
            _retiredKeyTimers.Clear();
        }
    }
}
