using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

public interface IQualityRejectRepository
{
    Task<QualityReject> AddAsync(QualityReject qualityReject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Total reject quantity across the given Machines (Story 3.1's supplementary figure alongside the
    /// loss pie chart). <paramref name="date"/> narrows to records on that UTC calendar date (Story 3.2);
    /// <c>null</c> means no date filter.
    /// </summary>
    Task<int> SumQuantityAsync(IReadOnlyList<Guid> machineIds, DateOnly? date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Total reject quantity across the given Machines whose RecordedAt falls in the arbitrary instant
    /// window [<paramref name="start"/>, <paramref name="end"/>) (Story 4.1's Shift/Day/Week report periods).
    /// </summary>
    Task<int> SumQuantityInRangeAsync(IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>Every record whose RecordedAt falls in (since, asOf] — the Sync module's "what's new" query (Story 5.1), not scoped to a machine list since sync pushes everything this local DB has.</summary>
    Task<IReadOnlyList<QualityReject>> ListRecordedSince(DateTimeOffset since, DateTimeOffset asOf, CancellationToken cancellationToken = default);
}
