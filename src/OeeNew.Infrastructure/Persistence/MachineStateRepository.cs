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
        var tracked = await context.MachineStates.FirstOrDefaultAsync(s => s.MachineId == state.MachineId, cancellationToken);
        if (tracked is null)
        {
            context.MachineStates.Add(state);
        }
        else if (!ReferenceEquals(tracked, state))
        {
            context.Entry(tracked).CurrentValues.SetValues(state);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
