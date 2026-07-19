using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Abstraction over Site persistence (AD-1: Application never calls EF Core directly).</summary>
public interface ISiteRepository
{
    Task<Site> AddAsync(Site site, CancellationToken cancellationToken = default);
    Task<Site?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(Site site, CancellationToken cancellationToken = default);
    Task DeleteAsync(Site site, CancellationToken cancellationToken = default);
}
