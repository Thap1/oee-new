using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>
/// Single ingestion path for every reading source — automatic adapters and any future manual-entry
/// caller both funnel through this one method (Story 2.1 AC #4; AD-3). Domain/Application never see
/// anything but the normalized <see cref="IProductionDataSource"/> shape.
/// </summary>
public sealed class IngestProductionReadingUseCase(
    IMachineRepository machines,
    ILineRepository lines,
    IMachineStateRepository machineStates,
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
        if (existing is null)
        {
            await machineStates.UpsertAsync(new MachineState(reading.MachineId, reading.Status, reading.Counter, reading.Timestamp), cancellationToken);
            await notifier.NotifyMachineStatusChangedAsync(reading.MachineId, reading.Status, reading.Counter, reading.Timestamp, cancellationToken);
            return;
        }

        var applied = existing.Apply(reading.Status, reading.Counter, reading.Timestamp);
        await machineStates.UpsertAsync(existing, cancellationToken);

        if (applied)
        {
            await notifier.NotifyMachineStatusChangedAsync(reading.MachineId, reading.Status, reading.Counter, reading.Timestamp, cancellationToken);
        }
    }
}
