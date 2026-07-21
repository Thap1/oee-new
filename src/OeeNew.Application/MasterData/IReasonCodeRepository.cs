using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Abstraction over ReasonCode persistence (AD-1: Application never calls EF Core directly).</summary>
public interface IReasonCodeRepository
{
    Task<ReasonCode> AddAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);
    Task<ReasonCode?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReasonCode>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default);
    Task UpdateAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete (Story 2.5, AC #5) — only reachable once <c>IDowntimeEventRepository.ExistsForReasonCodeAsync</c> confirms nothing references it.</summary>
    Task DeleteAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default);
}
