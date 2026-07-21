using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>
/// Single ingestion path for every reading source — automatic adapters and any future manual-entry
/// caller both funnel through this one method (Story 2.1 AC #4; AD-3). Domain/Application never see
/// anything but the normalized <see cref="IProductionDataSource"/> shape.
///
/// Story 2.5: also drives the <see cref="DowntimeEvent"/> lifecycle purely off status transitions —
/// opens on entry into <see cref="MachineStatus.Stopped"/>, closes on exit — so recorded downtime
/// duration reflects the machine's own reported timestamps, not Operator tap latency.
/// </summary>
public sealed class IngestProductionReadingUseCase(
    IMachineRepository machines,
    ILineRepository lines,
    IMachineStateRepository machineStates,
    IDowntimeEventRepository downtimeEvents,
    IMachineStatusNotifier notifier)
{
    public async Task IngestAsync(CallerScope scope, string? callerRole, IProductionDataSource reading, CancellationToken cancellationToken = default)
    {
        if (callerRole is not ("Operator" or "Admin"))
        {
            throw new MasterDataForbiddenException();
        }

        var machine = await machines.GetAsync(reading.MachineId, cancellationToken)
            ?? throw new MasterDataParentNotFoundException("Machine", reading.MachineId);

        if (!scope.IsGlobal)
        {
            var line = await lines.GetAsync(machine.LineId, cancellationToken);
            if (line is null || !scope.AllowsSite(line.SiteId) || !scope.AllowsLine(machine.LineId))
            {
                throw new MasterDataForbiddenException();
            }
        }

        var existing = await machineStates.GetAsync(reading.MachineId, cancellationToken);
        var previousStatus = existing?.Status;
        bool applied;

        if (existing is null)
        {
            await machineStates.UpsertAsync(new MachineState(reading.MachineId, reading.Status, reading.Counter, reading.Timestamp), cancellationToken);
            applied = true;
        }
        else
        {
            applied = existing.Apply(reading.Status, reading.Counter, reading.Timestamp);
            await machineStates.UpsertAsync(existing, cancellationToken);
        }

        if (!applied)
        {
            return;
        }

        await notifier.NotifyMachineStatusChangedAsync(reading.MachineId, reading.Status, reading.Counter, reading.Timestamp, cancellationToken);
        await HandleDowntimeTransitionAsync(reading.MachineId, previousStatus, reading.Status, reading.Timestamp, cancellationToken);
    }

    private async Task HandleDowntimeTransitionAsync(
        Guid machineId, MachineStatus? previousStatus, MachineStatus newStatus, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        if (newStatus == MachineStatus.Stopped && previousStatus != MachineStatus.Stopped)
        {
            await downtimeEvents.AddAsync(new DowntimeEvent(Guid.Empty, machineId, timestamp), cancellationToken);
        }
        else if (previousStatus == MachineStatus.Stopped && newStatus != MachineStatus.Stopped)
        {
            var openEvent = await downtimeEvents.GetOpenByMachineIdAsync(machineId, cancellationToken);
            if (openEvent is null)
            {
                return;
            }

            openEvent.Close(timestamp);
            await downtimeEvents.UpdateAsync(openEvent, cancellationToken);
        }
    }
}
