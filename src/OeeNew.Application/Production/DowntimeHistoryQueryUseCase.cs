using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;

namespace OeeNew.Application.Production;

/// <summary>One row of the Downtime history page (nav "Dừng máy") — a flattened, name-resolved view of a <see cref="Domain.Production.DowntimeEvent"/>, open or closed.</summary>
public sealed record DowntimeHistoryEntry(
    Guid Id,
    Guid MachineId,
    string MachineName,
    Guid? ReasonCodeId,
    string? ReasonCodeName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationSeconds);

/// <summary>Most recent downtime events across every Machine the caller is scoped to — same scope resolution as <see cref="MachineStatusQueryUseCase"/>.</summary>
public sealed class DowntimeHistoryQueryUseCase(
    IMachineRepository machines, IDowntimeEventRepository downtimeEvents, IReasonCodeRepository reasonCodes)
{
    private const int MaxResults = 200;

    public async Task<IReadOnlyList<DowntimeHistoryEntry>> ListAsync(CallerScope scope, CancellationToken cancellationToken = default)
    {
        var scopedMachines = await machines.ListByScopeAsync(scope, cancellationToken);
        if (scopedMachines.Count == 0)
        {
            return [];
        }

        var machineIds = scopedMachines.Select(m => m.Id).ToList();
        var events = await downtimeEvents.ListByMachineIdsAsync(machineIds, MaxResults, cancellationToken);
        if (events.Count == 0)
        {
            return [];
        }

        var machineNameById = scopedMachines.ToDictionary(m => m.Id, m => m.Name);

        var reasonCodeIds = events.Where(e => e.ReasonCodeId is not null).Select(e => e.ReasonCodeId!.Value).Distinct().ToList();
        var reasonCodeNameById = reasonCodeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await reasonCodes.ListByIdsAsync(reasonCodeIds, cancellationToken)).ToDictionary(r => r.Id, r => r.Name);

        return events
            .Select(e => new DowntimeHistoryEntry(
                e.Id,
                e.MachineId,
                machineNameById.GetValueOrDefault(e.MachineId, "?"),
                e.ReasonCodeId,
                e.ReasonCodeId is { } reasonCodeId ? reasonCodeNameById.GetValueOrDefault(reasonCodeId) : null,
                e.StartedAt,
                e.EndedAt,
                e.EndedAt is { } endedAt ? (long)(endedAt - e.StartedAt).TotalSeconds : null))
            .ToList();
    }
}
