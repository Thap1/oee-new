using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Production;
using OeeNew.Domain.MasterData;
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

    public async Task<bool> TryAssignReasonToOpenEventAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default)
    {
        var rows = await context.DowntimeEvents
            .Where(e => e.MachineId == machineId && e.EndedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ReasonCodeId, reasonCodeId), cancellationToken);
        return rows > 0;
    }

    public Task<bool> ExistsForReasonCodeAsync(Guid reasonCodeId, CancellationToken cancellationToken = default) =>
        context.DowntimeEvents.AnyAsync(e => e.ReasonCodeId == reasonCodeId, cancellationToken);

    public async Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesAsync(
        IReadOnlyList<Guid> machineIds, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var events = context.DowntimeEvents
            .Where(e => e.EndedAt != null && machineIds.Contains(e.MachineId));

        if (date is { } d)
        {
            var start = new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end = start.AddDays(1);
            events = events.Where(e => e.StartedAt >= start && e.StartedAt < end);
        }

        var query =
            from e in events
            join r in context.ReasonCodes on e.ReasonCodeId equals r.Id into reasonCodes
            from r in reasonCodes.DefaultIfEmpty()
            select new ClosedDowntimeSlice(
                e.MachineId,
                e.ReasonCodeId,
                r == null ? (LossCategory?)null : r.LossCategory,
                (long)(e.EndedAt!.Value - e.StartedAt).TotalSeconds);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesInRangeAsync(
        IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var events = context.DowntimeEvents
            .Where(e => e.EndedAt != null && machineIds.Contains(e.MachineId) && e.StartedAt >= start && e.StartedAt < end);

        var query =
            from e in events
            join r in context.ReasonCodes on e.ReasonCodeId equals r.Id into reasonCodes
            from r in reasonCodes.DefaultIfEmpty()
            select new ClosedDowntimeSlice(
                e.MachineId,
                e.ReasonCodeId,
                r == null ? (LossCategory?)null : r.LossCategory,
                (long)(e.EndedAt!.Value - e.StartedAt).TotalSeconds);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DowntimeEvent>> ListClosedSince(
        DateTimeOffset since, DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        await context.DowntimeEvents
            .Where(e => e.EndedAt != null && e.EndedAt > since && e.EndedAt <= asOf)
            .ToListAsync(cancellationToken);
}
