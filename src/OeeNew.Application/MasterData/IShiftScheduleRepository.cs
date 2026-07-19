using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Abstraction over ShiftSchedule persistence (AD-1: Application never calls EF Core directly).</summary>
public interface IShiftScheduleRepository
{
    Task<ShiftSchedule> AddAsync(ShiftSchedule shift, CancellationToken cancellationToken = default);
    Task<ShiftSchedule?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShiftSchedule>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShiftSchedule shift, CancellationToken cancellationToken = default);
    Task DeleteAsync(ShiftSchedule shift, CancellationToken cancellationToken = default);
}
