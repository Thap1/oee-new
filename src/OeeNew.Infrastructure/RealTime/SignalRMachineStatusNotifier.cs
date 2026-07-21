using Microsoft.AspNetCore.SignalR;
using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.RealTime;

/// <summary>Broadcasts to every client connected to this site's hub (AD-8 — no groups/per-scope filtering at the SignalR layer; see Story 2.2 Dev Notes).</summary>
public sealed class SignalRMachineStatusNotifier(IHubContext<MachineStatusHub> hubContext) : IMachineStatusNotifier
{
    public Task NotifyMachineStatusChangedAsync(Guid machineId, MachineStatus status, long counter, DateTimeOffset reportedAt, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync(
            "MachineStatusChanged",
            new { machineId, status, counter, lastReportedAt = reportedAt },
            cancellationToken);

    public Task NotifyDowntimeReasonRecordedAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync(
            "DowntimeReasonRecorded",
            new { machineId, reasonCodeId },
            cancellationToken);
}
