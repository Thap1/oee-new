using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>
/// Keeps <c>OeeNew.Application</c> ignorant of SignalR entirely (AD-1) — Infrastructure implements
/// this against the real-time hub (Story 2.2, AD-8).
/// </summary>
public interface IMachineStatusNotifier
{
    Task NotifyMachineStatusChangedAsync(Guid machineId, MachineStatus status, long counter, DateTimeOffset reportedAt, CancellationToken cancellationToken = default);

    /// <summary>Story 2.5 — event name `DowntimeReasonRecorded` is fixed by the Architecture Spine's Consistency Conventions table.</summary>
    Task NotifyDowntimeReasonRecordedAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default);
}
