using OeeNew.Application.MasterData;
using OeeNew.Domain.Identity;
using OeeNew.Domain.MasterData;

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

    public async Task<User> GetAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        return await users.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("User", id);
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

        // Validate the role/scope combination BEFORE provisioning the central credential — an
        // invalid combo (e.g. Operator with no Line) must fail here, not after a credential has
        // already been issued with no User row to attach it to.
        User.ValidateRoleAndScope(role, siteIds, lineIds);

        // Provision the credential at the central Identity Provider next, still before writing any
        // role-scoping. If this fails (central unreachable, AC #2), nothing has been persisted yet,
        // so there is no "orphaned" scoping row to roll back — see ICentralCredentialProvisioner.
        var passwordHash = await credentials.ProvisionAsync(username, password, cancellationToken);

        // A concurrent request for the same username can still race past the check above; the
        // repository maps that DB-level unique-index violation back to UsernameAlreadyTakenException
        // so the loser gets the correct 409 instead of a generic conflict (the credential just
        // provisioned for the loser is orphaned — same latent risk the central provisioner will need
        // a real compensating action for once it becomes a genuine network call).
        var user = new User(Guid.Empty, username, role, passwordHash, siteIds, lineIds);
        return await users.AddAsync(user, cancellationToken);
    }

    public async Task<User> UpdateRoleAndScopeAsync(
        string? callerRole, Guid id, UserRole role, Guid[] siteIds, Guid[] lineIds, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var user = await users.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("User", id);

        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            await EnsureNotLastAdminAsync(id, cancellationToken);
        }

        await EnsureScopeExistsAsync(siteIds, lineIds, cancellationToken);

        user.Rescope(role, siteIds, lineIds);
        await users.UpdateAsync(user, cancellationToken);
        return user;
    }

    /// <summary>Hide a user from login without deleting their role/scope history (Story 1.4 review — mirrors <c>ReasonCode.Deactivate</c>).</summary>
    public async Task<User> DeactivateAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var user = await users.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("User", id);

        if (user.Role == UserRole.Admin)
        {
            await EnsureNotLastAdminAsync(id, cancellationToken);
        }

        user.Deactivate();
        await users.UpdateAsync(user, cancellationToken);
        return user;
    }

    /// <summary>Story 1.4 review: refuse to demote/deactivate the only remaining active Admin — otherwise persisted-user Admin management could lock itself out.</summary>
    private async Task EnsureNotLastAdminAsync(Guid excludedUserId, CancellationToken cancellationToken)
    {
        var allUsers = await users.ListAsync(cancellationToken);
        var hasOtherActiveAdmin = allUsers.Any(u => u.Id != excludedUserId && u.Role == UserRole.Admin && u.IsActive);
        if (!hasOtherActiveAdmin)
        {
            throw new MasterDataValidationException("Cannot remove the last remaining Admin.");
        }
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
