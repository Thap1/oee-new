using Microsoft.AspNetCore.Identity;
using OeeNew.Application.Identity;
using OeeNew.Domain.Identity;

namespace OeeNew.Infrastructure.Identity;

/// <summary>
/// Central Identity Provider's credential-issuance implementation (AD-7). See
/// <see cref="ICentralCredentialProvisioner"/> for why this is a same-process hash today rather
/// than a network call, and what changes once true multi-instance deployment exists.
/// </summary>
public sealed class CentralCredentialProvisioner : ICentralCredentialProvisioner
{
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public Task<string> ProvisionAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_hasher.HashPassword(null!, password));
    }
}
