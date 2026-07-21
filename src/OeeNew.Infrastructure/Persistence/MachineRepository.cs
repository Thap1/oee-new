using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Infrastructure.Persistence;

public sealed class MachineRepository(OeeDbContext context) : IMachineRepository
{
    public async Task<Machine> AddAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        context.Machines.Add(machine);
        await context.SaveChangesAsync(cancellationToken);
        return machine;
    }

    public Task<Machine?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Machines.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Machine>> ListByLineAsync(Guid lineId, CancellationToken cancellationToken = default) =>
        await context.Machines.Where(m => m.LineId == lineId).OrderBy(m => m.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Machine>> ListByScopeAsync(CallerScope scope, CancellationToken cancellationToken = default)
    {
        var query = context.Machines.AsQueryable();

        if (!scope.IsGlobal)
        {
            var lineIdsInScope = context.Lines.Where(l => scope.SiteIds.Contains(l.SiteId)).Select(l => l.Id);
            query = query.Where(m => lineIdsInScope.Contains(m.LineId) && (scope.LineIds.Count == 0 || scope.LineIds.Contains(m.LineId)));
        }

        return await query.OrderBy(m => m.Name).ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(Machine machine, CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        context.Machines.Remove(machine);
        return context.SaveChangesAsync(cancellationToken);
    }
}
