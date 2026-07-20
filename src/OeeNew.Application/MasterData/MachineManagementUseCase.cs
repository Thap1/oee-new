using OeeNew.Application.Auth;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Create/rename/delete/list Machines under a Line (Story 1.2, AC #3 — FR-011; scope-filtered per Story 1.6, AC #2/#4). Admin-only for writes, re-checked here in addition to the API-layer policy.</summary>
public sealed class MachineManagementUseCase(IMachineRepository machines, ILineRepository lines)
{
    public async Task<IReadOnlyList<Machine>> ListByLineAsync(CallerScope scope, Guid lineId, CancellationToken cancellationToken = default)
    {
        if (!scope.IsGlobal)
        {
            // A nonexistent line has no machines either way — nothing to check, nothing to leak.
            var line = await lines.GetAsync(lineId, cancellationToken);
            if (line is not null && (!scope.AllowsSite(line.SiteId) || !scope.AllowsLine(lineId)))
            {
                throw new MasterDataForbiddenException();
            }
        }

        return await machines.ListByLineAsync(lineId, cancellationToken);
    }

    public async Task<Machine> CreateAsync(string? callerRole, Guid lineId, string name, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);

        var lineExists = await lines.GetAsync(lineId, cancellationToken) is not null;
        if (!lineExists)
        {
            throw new MasterDataParentNotFoundException("Line", lineId);
        }

        return await machines.AddAsync(new Machine(Guid.Empty, name, lineId), cancellationToken);
    }

    public async Task<Machine> RenameAsync(string? callerRole, Guid id, string name, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var machine = await machines.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("Machine", id);
        machine.Rename(name);
        await machines.UpdateAsync(machine, cancellationToken);
        return machine;
    }

    public async Task DeleteAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var machine = await machines.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("Machine", id);
        await machines.DeleteAsync(machine, cancellationToken);
    }
}
