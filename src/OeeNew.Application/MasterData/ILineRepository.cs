using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Abstraction over Line persistence (AD-1: Application never calls EF Core directly).</summary>
public interface ILineRepository
{
    Task<Line> AddAsync(Line line, CancellationToken cancellationToken = default);
    Task<Line?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Line>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Line line, CancellationToken cancellationToken = default);
    Task DeleteAsync(Line line, CancellationToken cancellationToken = default);
}
