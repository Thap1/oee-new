using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Tests.Production;

internal sealed class FakeQualityRejectRepository : IQualityRejectRepository
{
    public List<QualityReject> Added { get; } = [];

    public Task<QualityReject> AddAsync(QualityReject qualityReject, CancellationToken cancellationToken = default)
    {
        Added.Add(qualityReject);
        return Task.FromResult(qualityReject);
    }

    public Task<int> SumQuantityAsync(IReadOnlyList<Guid> machineIds, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var query = Added.Where(q => machineIds.Contains(q.MachineId));
        if (date is { } d)
        {
            query = query.Where(q => DateOnly.FromDateTime(q.RecordedAt.UtcDateTime) == d);
        }

        return Task.FromResult(query.Sum(q => q.Quantity));
    }

    public Task<int> SumQuantityInRangeAsync(
        IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var query = Added.Where(q => machineIds.Contains(q.MachineId) && q.RecordedAt >= start && q.RecordedAt < end);
        return Task.FromResult(query.Sum(q => q.Quantity));
    }
}
