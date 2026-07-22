using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Abstraction over ReasonCode persistence (AD-1: Application never calls EF Core directly).</summary>
public interface IReasonCodeRepository
{
    Task<ReasonCode> AddAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);
    Task<ReasonCode?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReasonCode>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default);

    /// <summary>One query for every requested ReasonCode (Story 3.2) — same batch-lookup shape as <see cref="ILineRepository.ListByIdsAsync"/>, avoids an N+1 loop over <see cref="GetAsync"/> when resolving names for a drill-down's grouped totals.</summary>
    Task<IReadOnlyList<ReasonCode>> ListByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task UpdateAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete (Story 2.5, AC #5) — only reachable once <c>IDowntimeEventRepository.ExistsForReasonCodeAsync</c> confirms nothing references it.</summary>
    Task DeleteAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);
}
