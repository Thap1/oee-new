using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>
/// One closed <see cref="DowntimeEvent"/>'s contribution to the Epic 3 loss pie chart (Story 3.1) — a
/// read-only aggregation projection, not the full entity. <see cref="LossCategory"/> is the attached
/// ReasonCode's category, or <c>null</c> for an unattributed event (Story 2.5 Dev Notes: closed before
/// an Operator picked a reason — an accepted outcome, not a defect). Invariant (Story 3.2): <see cref="LossCategory"/>
/// is non-null if and only if <see cref="ReasonCodeId"/> is non-null — a ReasonCode row is never
/// hard-deleted while any DowntimeEvent still references it (Story 2.5 AC #5).
/// </summary>
public sealed record ClosedDowntimeSlice(Guid MachineId, Guid? ReasonCodeId, LossCategory? LossCategory, long DurationSeconds);

public interface IDowntimeEventRepository
{
    Task<DowntimeEvent> AddAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default);

    Task<DowntimeEvent?> GetOpenByMachineIdAsync(Guid machineId, CancellationToken cancellationToken = default);

    Task UpdateAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically assigns a reason to the currently-open event for this machine, guarded by the same
    /// WHERE ... EndedAt IS NULL condition at the DB level — closes the TOCTOU window where a concurrent
    /// ingestion-triggered close could land between a read and a blind save. Returns false (no-op) if
    /// there was no open event to attach to, rather than throwing, so callers decide how to surface that.
    /// </summary>
    Task<bool> TryAssignReasonToOpenEventAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default);

    /// <summary>Used to guard hard-delete of a Reason Code (Story 1.5, extended by Story 2.5 AC #5).</summary>
    Task<bool> ExistsForReasonCodeAsync(Guid reasonCodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every closed (EndedAt != null) DowntimeEvent for the given Machines, one <see cref="ClosedDowntimeSlice"/>
    /// per event (Story 3.1's loss pie chart). <paramref name="date"/> narrows to events whose StartedAt falls
    /// on that UTC calendar date (Story 3.2); pass <c>null</c> for no date filter (Story 3.1's all-time view).
    /// </summary>
    Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesAsync(IReadOnlyList<Guid> machineIds, DateOnly? date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every closed DowntimeEvent for the given Machines whose StartedAt falls in the arbitrary instant
    /// window [<paramref name="start"/>, <paramref name="end"/>) (Story 4.1's Shift/Day/Week report periods —
    /// unlike <see cref="ListClosedSlicesAsync"/>'s single-calendar-day filter, a Shift may start mid-day and a
    /// Week spans 7 days).
    /// </summary>
    Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesInRangeAsync(
        IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}
