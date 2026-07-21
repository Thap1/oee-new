using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Abstraction over Line persistence (AD-1: Application never calls EF Core directly).</summary>
public interface ILineRepository
{
    Task<Line> AddAsync(Line line, CancellationToken cancellationToken = default);
    Task<Line?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Line>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default);

    /// <summary>One query for every requested Line (Story 2.5) — avoids an N+1 loop over <see cref="GetAsync"/> when resolving each Machine's Site.</summary>
    Task<IReadOnlyList<Line>> ListByIdsAsync(IReadOnlyList<Guid> lineIds, CancellationToken cancellationToken = default);
    Task UpdateAsync(Line line, CancellationToken cancellationToken = default);
    Task DeleteAsync(Line line, CancellationToken cancellationToken = default);
}
