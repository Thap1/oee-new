using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>
/// Attaches a Reason Code to an already-open `DowntimeEvent` (Story 2.5, FR-008/009). This use case
/// never opens or closes a `DowntimeEvent` — that lifecycle belongs entirely to
/// <see cref="IngestProductionReadingUseCase"/>, driven by machine status transitions.
/// </summary>
public sealed class RecordDowntimeReasonUseCase(
    IMachineRepository machines,
    ILineRepository lines,
    IReasonCodeRepository reasonCodes,
    IDowntimeEventRepository downtimeEvents,
    IMachineStatusNotifier notifier)
{
    public async Task AttachReasonAsync(
        CallerScope scope, string? callerRole, Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default)
    {
        if (callerRole is not ("Operator" or "Admin"))
        {
            throw new MasterDataForbiddenException();
        }

        var machine = await machines.GetAsync(machineId, cancellationToken)
            ?? throw new MasterDataParentNotFoundException("Machine", machineId);

        var line = await lines.GetAsync(machine.LineId, cancellationToken)
            ?? throw new MasterDataParentNotFoundException("Line", machine.LineId);

        if (!scope.IsGlobal && (!scope.AllowsSite(line.SiteId) || !scope.AllowsLine(machine.LineId)))
        {
            throw new MasterDataForbiddenException();
        }

        var reasonCode = await reasonCodes.GetAsync(reasonCodeId, cancellationToken)
            ?? throw new MasterDataParentNotFoundException("ReasonCode", reasonCodeId);

        if (!reasonCode.IsActive)
        {
            throw new MasterDataValidationException("This reason code is no longer active.");
        }

        if (reasonCode.SiteId != line.SiteId)
        {
            throw new MasterDataValidationException("This reason code does not belong to the machine's Site.");
        }

        var openEvent = await downtimeEvents.GetOpenByMachineIdAsync(machineId, cancellationToken)
            ?? throw new DowntimeEventNotOpenException();

        openEvent.AssignReason(reasonCodeId);
        await downtimeEvents.UpdateAsync(openEvent, cancellationToken);
        await notifier.NotifyDowntimeReasonRecordedAsync(machineId, reasonCodeId, cancellationToken);
    }
}
