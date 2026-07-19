using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Abstraction over Machine persistence (AD-1: Application never calls EF Core directly).</summary>
public interface IMachineRepository
{
    Task<Machine> AddAsync(Machine machine, CancellationToken cancellationToken = default);
    Task<Machine?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Machine>> ListByLineAsync(Guid lineId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Machine machine, CancellationToken cancellationToken = default);
    Task DeleteAsync(Machine machine, CancellationToken cancellationToken = default);
}
