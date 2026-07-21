using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Persistence;

public sealed class DowntimeEventRepository(OeeDbContext context) : IDowntimeEventRepository
{
    public async Task<DowntimeEvent> AddAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default)
    {
        context.DowntimeEvents.Add(downtimeEvent);
        await context.SaveChangesAsync(cancellationToken);
        return downtimeEvent;
    }

    public Task<DowntimeEvent?> GetOpenByMachineIdAsync(Guid machineId, CancellationToken cancellationToken = default) =>
        context.DowntimeEvents.FirstOrDefaultAsync(e => e.MachineId == machineId && e.EndedAt == null, cancellationToken);

    public Task UpdateAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task<bool> ExistsForReasonCodeAsync(Guid reasonCodeId, CancellationToken cancellationToken = default) =>
        context.DowntimeEvents.AnyAsync(e => e.ReasonCodeId == reasonCodeId, cancellationToken);
}
