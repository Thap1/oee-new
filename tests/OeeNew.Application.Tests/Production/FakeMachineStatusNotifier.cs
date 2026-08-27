using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Tests.Production;

internal sealed class FakeMachineStatusNotifier : IMachineStatusNotifier
{
    public List<(Guid MachineId, MachineStatus Status, long Counter, DateTimeOffset ReportedAt)> Calls { get; } = [];
    public List<(Guid MachineId, Guid ReasonCodeId)> DowntimeReasonCalls { get; } = [];

    public Task NotifyMachineStatusChangedAsync(Guid machineId, MachineStatus status, long counter, DateTimeOffset reportedAt, CancellationToken cancellationToken = default)
    {
        Calls.Add((machineId, status, counter, reportedAt));
        return Task.CompletedTask;
    }

    public Task NotifyMachineStatusesChangedAsync(IReadOnlyList<MachineStatusChange> changes, CancellationToken cancellationToken = default)
    {
        foreach (var change in changes)
        {
            Calls.Add((change.MachineId, change.Status, change.Counter, change.ReportedAt));
        }

        return Task.CompletedTask;
    }

    public Task NotifyDowntimeReasonRecordedAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default)
    {
        DowntimeReasonCalls.Add((machineId, reasonCodeId));
        return Task.CompletedTask;
    }
}
