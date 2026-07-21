using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

public interface IDowntimeEventRepository
{
    Task<DowntimeEvent> AddAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default);

    Task<DowntimeEvent?> GetOpenByMachineIdAsync(Guid machineId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default);

    /// <summary>Used to guard hard-delete of a Reason Code (Story 1.5, extended by Story 2.5 AC #5).</summary>
    Task<bool> ExistsForReasonCodeAsync(Guid reasonCodeId, CancellationToken cancellationToken = default);
}
