using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Tests.Production;

internal sealed class FakeMachineStatusNotifier : IMachineStatusNotifier
{
    public List<(Guid MachineId, MachineStatus Status, long Counter, DateTimeOffset ReportedAt)> Calls { get; } = [];

    public Task NotifyMachineStatusChangedAsync(Guid machineId, MachineStatus status, long counter, DateTimeOffset reportedAt, CancellationToken cancellationToken = default)
    {
        Calls.Add((machineId, status, counter, reportedAt));
        return Task.CompletedTask;
    }
}
