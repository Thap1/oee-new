using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Create/rename/delete/list Lines under a Site (Story 1.2, AC #2, #5 — FR-011). Admin-only, re-checked here in addition to the API-layer policy.</summary>
public sealed class LineManagementUseCase(ILineRepository lines, ISiteRepository sites, IMachineRepository machines)
{
    public Task<IReadOnlyList<Line>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        lines.ListBySiteAsync(siteId, cancellationToken);

    public async Task<Line> CreateAsync(string? callerRole, Guid siteId, string name, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);

        var siteExists = await sites.GetAsync(siteId, cancellationToken) is not null;
        if (!siteExists)
        {
            throw new MasterDataParentNotFoundException("Site", siteId);
        }

        return await lines.AddAsync(new Line(Guid.Empty, name, siteId), cancellationToken);
    }

    public async Task<Line> RenameAsync(string? callerRole, Guid id, string name, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var line = await lines.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("Line", id);
        line.Rename(name);
        await lines.UpdateAsync(line, cancellationToken);
        return line;
    }

    public async Task DeleteAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var line = await lines.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("Line", id);

        var dependentMachines = await machines.ListByLineAsync(id, cancellationToken);
        if (dependentMachines.Count > 0)
        {
            throw new MasterDataHasDependentsException("Line", id, dependentMachines.Select(m => m.Name).ToList());
        }

        await lines.DeleteAsync(line, cancellationToken);
    }
}
