using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

public interface IMachineStateRepository
{
    Task<MachineState?> GetAsync(Guid machineId, CancellationToken cancellationToken = default);

    /// <summary>One query for every requested machine (Story 2.2) — avoids an N+1 loop over <see cref="GetAsync"/>.</summary>
    Task<IReadOnlyList<MachineState>> ListByMachineIdsAsync(IReadOnlyList<Guid> machineIds, CancellationToken cancellationToken = default);

    Task UpsertAsync(MachineState state, CancellationToken cancellationToken = default);
}
