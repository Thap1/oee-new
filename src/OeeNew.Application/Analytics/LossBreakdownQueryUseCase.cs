using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.Analytics;

/// <summary>
/// Loss pie chart data for one Equipment (Machine) or Production Area (Line) (Story 3.1, FR-019/020).
/// Scope-enforced the same way as <see cref="LineManagementUseCase.ListBySiteAsync"/> — <paramref name="targetId"/>
/// in <see cref="GetAsync"/> is an explicit, spoofable parameter, unlike Story 2.2's scope-derived
/// <c>machine-states</c> endpoint, so it is checked against the caller's <see cref="CallerScope"/> before use.
/// </summary>
public sealed class LossBreakdownQueryUseCase(
    IMachineRepository machines,
    ILineRepository lines,
    IDowntimeEventRepository downtimeEvents,
    IQualityRejectRepository qualityRejects,
    IReasonCodeRepository reasonCodes)
{
    public async Task<LossBreakdownResult> GetAsync(
        CallerScope scope, LossBreakdownTargetType targetType, Guid targetId, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var machineIds = targetType == LossBreakdownTargetType.Equipment
            ? await ResolveEquipmentAsync(scope, targetId, cancellationToken)
            : await ResolveAreaAsync(scope, targetId, cancellationToken);

        if (machineIds.Count == 0)
        {
            return new LossBreakdownResult(targetId, targetType, 0, 0, 0, 0, 0);
        }

        var slices = await downtimeEvents.ListClosedSlicesAsync(machineIds, date, cancellationToken);
        var availability = SumFor(slices, LossCategory.AvailabilityLoss);
        var performance = SumFor(slices, LossCategory.PerformanceLoss);
        var quality = SumFor(slices, LossCategory.QualityLoss);
        var unattributed = slices.Where(s => s.LossCategory is null).Sum(s => s.DurationSeconds);

        var rejectQuantity = await qualityRejects.SumQuantityAsync(machineIds, date, cancellationToken);

        return new LossBreakdownResult(targetId, targetType, availability, performance, quality, unattributed, rejectQuantity);
    }

    /// <summary>
    /// Reason-code breakdown within one loss category for a tapped pie slice (Story 3.2, AC #2).
    /// Reuses <see cref="ResolveEquipmentAsync"/>/<see cref="ResolveAreaAsync"/> — same scope check as
    /// <see cref="GetAsync"/>, not duplicated in a separate use case class.
    /// </summary>
    public async Task<IReadOnlyList<ReasonBreakdownItem>> GetReasonBreakdownAsync(
        CallerScope scope, LossBreakdownTargetType targetType, Guid targetId, LossCategory lossCategory, DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var machineIds = targetType == LossBreakdownTargetType.Equipment
            ? await ResolveEquipmentAsync(scope, targetId, cancellationToken)
            : await ResolveAreaAsync(scope, targetId, cancellationToken);

        if (machineIds.Count == 0)
        {
            return [];
        }

        var slices = await downtimeEvents.ListClosedSlicesAsync(machineIds, date, cancellationToken);
        // Invariant (see ClosedDowntimeSlice): LossCategory is non-null iff ReasonCodeId is non-null,
        // so every row surviving this filter has a ReasonCodeId to group by.
        var totalsByReasonCodeId = slices
            .Where(s => s.LossCategory == lossCategory)
            .GroupBy(s => s.ReasonCodeId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationSeconds));

        if (totalsByReasonCodeId.Count == 0)
        {
            return [];
        }

        var matchingReasonCodes = await reasonCodes.ListByIdsAsync(totalsByReasonCodeId.Keys.ToList(), cancellationToken);

        return matchingReasonCodes
            .Select(r => new ReasonBreakdownItem(r.Id, r.Name, totalsByReasonCodeId[r.Id]))
            .OrderByDescending(item => item.DurationSeconds)
            .ToList();
    }

    private async Task<IReadOnlyList<Guid>> ResolveEquipmentAsync(CallerScope scope, Guid machineId, CancellationToken cancellationToken)
    {
        var machine = await machines.GetAsync(machineId, cancellationToken) ?? throw new MasterDataNotFoundException("Machine", machineId);
        var line = await lines.GetAsync(machine.LineId, cancellationToken);
        if (line is null || !scope.AllowsSite(line.SiteId) || !scope.AllowsLine(machine.LineId))
        {
            throw new MasterDataForbiddenException();
        }

        return [machine.Id];
    }

    private async Task<IReadOnlyList<Guid>> ResolveAreaAsync(CallerScope scope, Guid lineId, CancellationToken cancellationToken)
    {
        var line = await lines.GetAsync(lineId, cancellationToken) ?? throw new MasterDataNotFoundException("Line", lineId);
        if (!scope.AllowsSite(line.SiteId) || !scope.AllowsLine(lineId))
        {
            throw new MasterDataForbiddenException();
        }

        var lineMachines = await machines.ListByLineAsync(lineId, cancellationToken);
        return lineMachines.Select(m => m.Id).ToList();
    }

    private static long SumFor(IReadOnlyList<ClosedDowntimeSlice> slices, LossCategory category) =>
        slices.Where(s => s.LossCategory == category).Sum(s => s.DurationSeconds);
}
