using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Tests.Production;

internal sealed class FakeMachineStateRepository : IMachineStateRepository
{
    private readonly Dictionary<Guid, MachineState> _states = new();

    public Task<MachineState?> GetAsync(Guid machineId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_states.GetValueOrDefault(machineId));

    public Task<IReadOnlyList<MachineState>> ListByMachineIdsAsync(IReadOnlyList<Guid> machineIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MachineState>>(_states.Values.Where(s => machineIds.Contains(s.MachineId)).ToList());

    public Task UpsertAsync(MachineState state, CancellationToken cancellationToken = default)
    {
        _states[state.MachineId] = state;
        return Task.CompletedTask;
    }
}
