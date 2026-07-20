using OeeNew.Application.Auth;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Create/rename/delete/list Sites (Story 1.2, AC #1, #5 — FR-011; scope-filtered per Story 1.6, AC #2/#4). Admin-only for writes, re-checked here in addition to the API-layer policy.</summary>
public sealed class SiteManagementUseCase(ISiteRepository sites, ILineRepository lines)
{
    public async Task<IReadOnlyList<Site>> ListAsync(CallerScope scope, CancellationToken cancellationToken = default)
    {
        var all = await sites.ListAsync(cancellationToken);
        return scope.IsGlobal ? all : all.Where(s => scope.AllowsSite(s.Id)).ToList();
    }

    public Task<Site> CreateAsync(string? callerRole, string name, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        return sites.AddAsync(new Site(Guid.Empty, name), cancellationToken);
    }

    public async Task<Site> RenameAsync(string? callerRole, Guid id, string name, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var site = await sites.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("Site", id);
        site.Rename(name);
        await sites.UpdateAsync(site, cancellationToken);
        return site;
    }

    public async Task DeleteAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var site = await sites.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("Site", id);

        var dependentLines = await lines.ListBySiteAsync(id, cancellationToken);
        if (dependentLines.Count > 0)
        {
            throw new MasterDataHasDependentsException("Site", id, dependentLines.Select(l => l.Name).ToList());
        }

        await sites.DeleteAsync(site, cancellationToken);
    }
}
