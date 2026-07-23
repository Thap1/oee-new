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
/// <see cref="StartedAt"/>/<see cref="EndedAt"/> (added for Story 4.1's range-based reports) let a
/// consumer clip this slice's contribution to a sub-window (e.g. a Shift's narrow instant range) instead
/// of trusting <see cref="DurationSeconds"/> whole — see <see cref="ListClosedSlicesInRangeAsync"/>.
/// </summary>
public sealed record ClosedDowntimeSlice(Guid MachineId, Guid? ReasonCodeId, LossCategory? LossCategory, long DurationSeconds, DateTimeOffset StartedAt, DateTimeOffset EndedAt);

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
    /// Every closed DowntimeEvent for the given Machines that <b>overlaps</b> the arbitrary instant window
    /// [<paramref name="start"/>, <paramref name="end"/>) (Story 4.1's Shift/Day/Week report periods —
    /// unlike <see cref="ListClosedSlicesAsync"/>'s single-calendar-day filter, a Shift may start mid-day and a
    /// Week spans 7 days). Unlike <see cref="ListClosedSlicesAsync"/>'s StartedAt-only filter, this returns an
    /// event whose <c>StartedAt</c> is before <paramref name="start"/> but whose <c>EndedAt</c> falls inside the
    /// window (and vice versa) — the caller is expected to clip each slice's <see cref="ClosedDowntimeSlice.DurationSeconds"/>
    /// contribution to the window using its <see cref="ClosedDowntimeSlice.StartedAt"/>/<see cref="ClosedDowntimeSlice.EndedAt"/>,
    /// since a narrow Shift window makes boundary-spanning events common (code-review fix, Epic 4).
    /// </summary>
    Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesInRangeAsync(
        IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>Closed events (EndedAt != null) whose EndedAt falls in (since, asOf] — the Sync module's "what's new" query (Story 5.1). Unlike <see cref="ListClosedSlicesInRangeAsync"/>, returns full entities (ReasonCodeId/StartedAt/EndedAt all needed on the wire), not the <see cref="ClosedDowntimeSlice"/> projection, and is NOT scoped to a machine list — sync pushes everything this local DB has.</summary>
    Task<IReadOnlyList<DowntimeEvent>> ListClosedSince(DateTimeOffset since, DateTimeOffset asOf, CancellationToken cancellationToken = default);
}
