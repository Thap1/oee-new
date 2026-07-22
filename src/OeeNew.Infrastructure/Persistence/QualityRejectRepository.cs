using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Persistence;

public sealed class QualityRejectRepository(OeeDbContext context) : IQualityRejectRepository
{
    public async Task<QualityReject> AddAsync(QualityReject qualityReject, CancellationToken cancellationToken = default)
    {
        context.QualityRejects.Add(qualityReject);
        await context.SaveChangesAsync(cancellationToken);
        return qualityReject;
    }

    public async Task<int> SumQuantityAsync(IReadOnlyList<Guid> machineIds, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var query = context.QualityRejects.Where(q => machineIds.Contains(q.MachineId));

        if (date is { } d)
        {
            var start = new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end = start.AddDays(1);
            query = query.Where(q => q.RecordedAt >= start && q.RecordedAt < end);
        }

        return await query.SumAsync(q => (int?)q.Quantity, cancellationToken) ?? 0;
    }

    public async Task<int> SumQuantityInRangeAsync(
        IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var query = context.QualityRejects
            .Where(q => machineIds.Contains(q.MachineId) && q.RecordedAt >= start && q.RecordedAt < end);

        return await query.SumAsync(q => (int?)q.Quantity, cancellationToken) ?? 0;
    }
}
