using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;

namespace OeeNew.Application.Production;

/// <summary>Current status of every Machine the caller is scoped to (Story 2.2; reused verbatim by Story 2.4's multi-machine dashboard).</summary>
public sealed class MachineStatusQueryUseCase(IMachineRepository machines, IMachineStateRepository machineStates)
{
    public async Task<IReadOnlyList<MachineStatusSnapshot>> ListAsync(CallerScope scope, CancellationToken cancellationToken = default)
    {
        var scopedMachines = await machines.ListByScopeAsync(scope, cancellationToken);
        if (scopedMachines.Count == 0)
        {
            return [];
        }

        var machineIds = scopedMachines.Select(m => m.Id).ToList();
        var states = await machineStates.ListByMachineIdsAsync(machineIds, cancellationToken);
        var statesByMachineId = states.ToDictionary(s => s.MachineId);

        return scopedMachines
            .Select(machine =>
            {
                var state = statesByMachineId.GetValueOrDefault(machine.Id);
                return new MachineStatusSnapshot(machine.Id, machine.Name, machine.LineId, state?.Status, state?.Counter, state?.LastReportedAt);
            })
            .ToList();
    }
}
