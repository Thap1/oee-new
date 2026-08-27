using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>
/// Keeps <c>OeeNew.Application</c> ignorant of SignalR entirely (AD-1) — Infrastructure implements
/// this against the real-time hub (Story 2.2, AD-8).
/// </summary>
public interface IMachineStatusNotifier
{
    Task NotifyMachineStatusChangedAsync(Guid machineId, MachineStatus status, long counter, DateTimeOffset reportedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// One grouped broadcast for many machines at once — for a caller (e.g. the demo signal
    /// simulator) that changes a whole fleet within the same tick, so clients get one message and
    /// one re-render instead of one per machine.
    /// </summary>
    Task NotifyMachineStatusesChangedAsync(IReadOnlyList<MachineStatusChange> changes, CancellationToken cancellationToken = default);

    /// <summary>Story 2.5 — event name `DowntimeReasonRecorded` is fixed by the Architecture Spine's Consistency Conventions table.</summary>
    Task NotifyDowntimeReasonRecordedAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default);
}

public readonly record struct MachineStatusChange(Guid MachineId, MachineStatus Status, long Counter, DateTimeOffset ReportedAt);
