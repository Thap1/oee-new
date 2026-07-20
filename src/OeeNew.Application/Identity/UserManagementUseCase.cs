using OeeNew.Application.MasterData;
using OeeNew.Domain.Identity;

namespace OeeNew.Application.Identity;

/// <summary>
/// Create/re-scope Users with role + Site/Line assignment (Story 1.4, AC #1, #2, #4 — FR-013).
/// Admin-only, re-checked here in addition to the API-layer policy.
/// </summary>
public sealed class UserManagementUseCase(
    IUserRepository users, ISiteRepository sites, ILineRepository lines, ICentralCredentialProvisioner credentials)
{
    public Task<IReadOnlyList<User>> ListAsync(string? callerRole, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        return users.ListAsync(cancellationToken);
    }

    public async Task<User> CreateAsync(
        string? callerRole, string username, string password, UserRole role,
        Guid[] siteIds, Guid[] lineIds, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);

        if (await users.GetByUsernameAsync(username, cancellationToken) is not null)
        {
            throw new UsernameAlreadyTakenException(username);
        }

        await EnsureScopeExistsAsync(siteIds, lineIds, cancellationToken);

        // Provision the credential at the central Identity Provider FIRST, before writing any
        // role-scoping. If this fails (central unreachable, AC #2), nothing has been persisted yet,
        // so there is no "orphaned" scoping row to roll back — see ICentralCredentialProvisioner.
        var passwordHash = await credentials.ProvisionAsync(username, password, cancellationToken);

        var user = new User(Guid.Empty, username, role, passwordHash, siteIds, lineIds);
        return await users.AddAsync(user, cancellationToken);
    }

    public async Task<User> UpdateRoleAndScopeAsync(
        string? callerRole, Guid id, UserRole role, Guid[] siteIds, Guid[] lineIds, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var user = await users.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("User", id);

        await EnsureScopeExistsAsync(siteIds, lineIds, cancellationToken);

        user.Rescope(role, siteIds, lineIds);
        await users.UpdateAsync(user, cancellationToken);
        return user;
    }

    private async Task EnsureScopeExistsAsync(Guid[] siteIds, Guid[] lineIds, CancellationToken cancellationToken)
    {
        foreach (var siteId in siteIds)
        {
            if (await sites.GetAsync(siteId, cancellationToken) is null)
            {
                throw new MasterDataParentNotFoundException("Site", siteId);
            }
        }

        foreach (var lineId in lineIds)
        {
            var line = await lines.GetAsync(lineId, cancellationToken);
            if (line is null || !siteIds.Contains(line.SiteId))
            {
                throw new MasterDataParentNotFoundException("Line", lineId);
            }
        }
    }
}
