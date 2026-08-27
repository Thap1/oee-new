using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Persistence;

public sealed class MachineStateRepository(OeeDbContext context) : IMachineStateRepository
{
    public Task<MachineState?> GetAsync(Guid machineId, CancellationToken cancellationToken = default) =>
        context.MachineStates.FirstOrDefaultAsync(s => s.MachineId == machineId, cancellationToken);

    public async Task<IReadOnlyList<MachineState>> ListByMachineIdsAsync(IReadOnlyList<Guid> machineIds, CancellationToken cancellationToken = default) =>
        await context.MachineStates.Where(s => machineIds.Contains(s.MachineId)).ToListAsync(cancellationToken);

    public async Task UpsertAsync(MachineState state, CancellationToken cancellationToken = default)
    {
        // Callers that already hold a tracked instance (e.g. loaded via GetAsync earlier in the same
        // scope, then mutated) need no lookup here — EF's change tracker already has the pending update.
        // Re-querying it was a redundant round trip on the hot ingest path.
        if (context.Entry(state).State == EntityState.Detached)
        {
            var tracked = await context.MachineStates.FindAsync([state.MachineId], cancellationToken);
            if (tracked is null)
            {
                context.MachineStates.Add(state);
            }
            else
            {
                context.Entry(tracked).CurrentValues.SetValues(state);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
