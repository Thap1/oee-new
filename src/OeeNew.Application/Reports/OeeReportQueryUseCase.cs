using System.Globalization;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.Reports;

/// <summary>
/// Aggregated OEE report over a Shift/Day/Week period (Story 4.1, FR-016), optionally narrowed to a
/// Site/Line/Machine filter within the caller's scope (Story 4.2, FR-017). Computes a
/// <b>time-based-loss proxy</b>, not textbook count-based OEE — there is no ProductionCount/ideal-cycle-time
/// entity anywhere in the codebase to compute a true Performance/Quality ratio from, so this reuses exactly
/// the same "seconds lost per LossCategory" data Epic 3's <see cref="Analytics.LossBreakdownQueryUseCase"/>
/// already established:
/// <code>
/// Availability% = (Planned − AvailabilityLoss) / Planned
/// Performance%  = (Planned − AvailabilityLoss − PerformanceLoss) / (Planned − AvailabilityLoss)
/// Quality%      = (Planned − AvailabilityLoss − PerformanceLoss − QualityLoss) / (Planned − AvailabilityLoss − PerformanceLoss)
/// OEE% = Availability% × Performance% × Quality%
/// </code>
/// A stage whose denominator is 0 (100% of the preceding time already lost) reports 0, not NaN.
/// <see cref="ResolvePeriod"/>/<see cref="ResolveMachinesAsync"/> are split out as reusable building
/// blocks — Story 4.3's top-downtime-reason report calls back into this same class rather than duplicating them.
/// </summary>
public sealed class OeeReportQueryUseCase(
    ISiteRepository sites,
    IMachineRepository machines,
    ILineRepository lines,
    IShiftScheduleRepository shiftSchedules,
    IDowntimeEventRepository downtimeEvents,
    IQualityRejectRepository qualityRejects,
    IReasonCodeRepository reasonCodes)
{
    public async Task<OeeReportResult> GetReportAsync(
        CallerScope scope, ReportPeriodType periodType, DateOnly referenceDate, Guid? shiftScheduleId,
        ReportFilterTargetType? filterType = null, Guid? filterId = null, CancellationToken cancellationToken = default)
    {
        // Code-review fix: the picked ShiftSchedule is fetched and scope-checked exactly once here (was
        // previously fetched + re-authorized independently inside both ResolvePeriodAsync and
        // ResolveMachinesAsync) and threaded through to both — removes a redundant DB round-trip and a
        // narrow TOCTOU window where the two independent checks could observe different shift states.
        var shift = periodType == ReportPeriodType.Shift
            ? await ResolveAuthorizedShiftAsync(scope, shiftScheduleId!.Value, cancellationToken)
            : null;

        var (start, end, plannedSecondsPerMachine) = ResolvePeriod(periodType, referenceDate, shift);
        var machineIds = await ResolveMachinesAsync(scope, periodType, shift, cancellationToken);

        if (filterType is { } type)
        {
            // Checked against CallerScope (not the narrower period-implied set) — a filter that's
            // legitimately scoped but disjoint from the period (e.g. a Site B filter on a Site A shift)
            // is a normal empty result, not a forbidden request. Only an out-of-CallerScope filter is
            // the AC #3 rejection (Story 4.2 Dev Notes: these are two independent checks, don't merge them).
            var filterMachineIds = await ResolveFilterMachinesAsync(scope, type, filterId!.Value, cancellationToken);
            var filterSet = filterMachineIds.ToHashSet();
            machineIds = machineIds.Where(filterSet.Contains).ToList();
        }

        if (machineIds.Count == 0)
        {
            return new OeeReportResult(periodType, start, end, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        // Code-review fix: ResolvePeriod's plannedSeconds is a single machine's planned time budget
        // (one day/week/shift), but Day/Week (and any multi-machine filter) can aggregate across many
        // machines at once — dividing an N-machine loss total by a 1-machine time budget produced
        // nonsensical/negative percentages for every caller with more than one machine in scope, which is
        // the default, unfiltered case AC #1 asks for. Scaling the denominator by machine count is the
        // standard multi-asset OEE aggregation convention (total available machine-time, not one machine's).
        var plannedSeconds = plannedSecondsPerMachine * machineIds.Count;

        var slices = await downtimeEvents.ListClosedSlicesInRangeAsync(machineIds, start, end, cancellationToken);
        var availabilityLoss = SumFor(slices, LossCategory.AvailabilityLoss, start, end);
        var performanceLoss = SumFor(slices, LossCategory.PerformanceLoss, start, end);
        var qualityLoss = SumFor(slices, LossCategory.QualityLoss, start, end);
        var unattributed = slices.Where(s => s.LossCategory is null).Sum(s => ClippedSeconds(s, start, end));

        var rejectQuantity = await qualityRejects.SumQuantityInRangeAsync(machineIds, start, end, cancellationToken);

        var afterAvailability = plannedSeconds - availabilityLoss;
        var afterPerformance = afterAvailability - performanceLoss;
        var afterQuality = afterPerformance - qualityLoss;

        var availabilityPercent = RatioOrZero(afterAvailability, plannedSeconds);
        var performancePercent = RatioOrZero(afterPerformance, afterAvailability);
        var qualityPercent = RatioOrZero(afterQuality, afterPerformance);
        var oeePercent = availabilityPercent * performancePercent * qualityPercent;

        var topReason = await ResolveTopDowntimeReasonAsync(slices, start, end, cancellationToken);

        return new OeeReportResult(
            periodType, start, end,
            availabilityPercent, performancePercent, qualityPercent, oeePercent,
            availabilityLoss, performanceLoss, qualityLoss, unattributed, rejectQuantity,
            topReason?.Id, topReason?.Name, topReason?.Seconds);
    }

    /// <summary>Fetches the picked <see cref="ShiftSchedule"/> and checks it against <see cref="CallerScope"/> — the single authorization point for a Shift-period request (see Code-review fix note on <see cref="GetReportAsync"/>).</summary>
    private async Task<ShiftSchedule> ResolveAuthorizedShiftAsync(CallerScope scope, Guid shiftScheduleId, CancellationToken cancellationToken)
    {
        var shift = await shiftSchedules.GetAsync(shiftScheduleId, cancellationToken)
            ?? throw new MasterDataNotFoundException("ShiftSchedule", shiftScheduleId);
        if (!scope.AllowsSite(shift.SiteId) || (shift.LineId is { } lineId && !scope.AllowsLine(lineId)))
        {
            throw new MasterDataForbiddenException();
        }

        return shift;
    }

    /// <summary>
    /// The single downtime reason with the most total seconds across the whole period (Story 4.3,
    /// FR-018) — grouped across all three <see cref="LossCategory"/> values together, unlike
    /// <see cref="Analytics.LossBreakdownQueryUseCase.GetReasonBreakdownAsync"/>'s single-category
    /// drill-down. Ties break by name (ordinal, locale-independent) for view-to-view stability (AC #2).
    /// <c>null</c> when no attributed downtime exists in the period (AC #3).
    /// </summary>
    private async Task<(Guid Id, string Name, long Seconds)?> ResolveTopDowntimeReasonAsync(
        IReadOnlyList<ClosedDowntimeSlice> slices, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var totalsByReasonCodeId = slices
            .Where(s => s.ReasonCodeId is not null)
            .GroupBy(s => s.ReasonCodeId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(s => ClippedSeconds(s, start, end)));

        if (totalsByReasonCodeId.Count == 0)
        {
            return null;
        }

        // ReasonCode.Name has no uniqueness constraint, so a name-only tie-break (AC #2's literal wording)
        // isn't fully deterministic if two reason codes ever share a name — Id is a final, always-unique
        // tiebreaker so the result never depends on ListByIdsAsync's enumeration order in that residual case.
        var matchingReasonCodes = await reasonCodes.ListByIdsAsync(totalsByReasonCodeId.Keys.ToList(), cancellationToken);

        var ranked = matchingReasonCodes
            .Select(r => (r.Id, r.Name, Seconds: totalsByReasonCodeId[r.Id]))
            .OrderByDescending(t => t.Seconds)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ThenBy(t => t.Id)
            .ToList();

        // ReasonCode rows are never hard-deleted while a DowntimeEvent still references them (Story 2.5
        // AC #5 invariant, documented on ClosedDowntimeSlice), so ListByIdsAsync is expected to resolve
        // every id in totalsByReasonCodeId — ranked.Count should equal totalsByReasonCodeId.Count. If that
        // invariant is ever violated, this silently ranks over a reduced candidate set rather than failing loudly.
        return ranked.Count > 0 ? ranked[0] : null;
    }

    /// <summary>
    /// Resolves the period's absolute <c>[start, end)</c> instant window and its planned-time denominator.
    /// Day/Week use the full UTC calendar period (no per-site "operating hours" concept exists in master
    /// data); Shift combines <paramref name="shift"/>'s time-of-day window with <paramref name="referenceDate"/>,
    /// handling the overnight-wrap case the same way <see cref="ShiftSchedule.OverlapsWith"/> does.
    /// <paramref name="shift"/> must already be authorized (see <see cref="ResolveAuthorizedShiftAsync"/>) —
    /// this method trusts it and does not re-check <see cref="CallerScope"/>.
    /// </summary>
    internal (DateTimeOffset Start, DateTimeOffset End, long PlannedSeconds) ResolvePeriod(
        ReportPeriodType periodType, DateOnly referenceDate, ShiftSchedule? shift)
    {
        switch (periodType)
        {
            case ReportPeriodType.Day:
            {
                var start = new DateTimeOffset(referenceDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                var end = start.AddDays(1);
                return (start, end, 86_400);
            }
            case ReportPeriodType.Week:
            {
                var year = ISOWeek.GetYear(referenceDate.ToDateTime(TimeOnly.MinValue));
                var week = ISOWeek.GetWeekOfYear(referenceDate.ToDateTime(TimeOnly.MinValue));
                var monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
                var start = new DateTimeOffset(DateTime.SpecifyKind(monday, DateTimeKind.Unspecified), TimeSpan.Zero);
                var end = start.AddDays(7);
                return (start, end, 7 * 86_400L);
            }
            case ReportPeriodType.Shift:
            {
                var referenceMidnight = DateTime.SpecifyKind(referenceDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                var start = new DateTimeOffset(referenceMidnight, TimeSpan.Zero) + shift!.StartTime.ToTimeSpan();
                var end = shift.EndTime > shift.StartTime
                    ? new DateTimeOffset(referenceMidnight, TimeSpan.Zero) + shift.EndTime.ToTimeSpan()
                    : new DateTimeOffset(referenceMidnight.AddDays(1), TimeSpan.Zero) + shift.EndTime.ToTimeSpan();
                return (start, end, (long)(end - start).TotalSeconds);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(periodType), periodType, null);
        }
    }

    /// <summary>
    /// Resolves every Machine the report should aggregate over before any Story 4.2 filter is applied.
    /// Day/Week fall back to the caller's full scope; Shift resolves through <paramref name="shift"/>'s
    /// Site/Line, already authorized by <see cref="ResolveAuthorizedShiftAsync"/> — the same reasoning
    /// <see cref="Analytics.LossBreakdownQueryUseCase"/>'s ResolveEquipmentAsync/ResolveAreaAsync use,
    /// just checked once up front instead of again here.
    /// </summary>
    internal async Task<IReadOnlyList<Guid>> ResolveMachinesAsync(
        CallerScope scope, ReportPeriodType periodType, ShiftSchedule? shift, CancellationToken cancellationToken)
    {
        if (periodType != ReportPeriodType.Shift)
        {
            var scoped = await machines.ListByScopeAsync(scope, cancellationToken);
            return scoped.Select(m => m.Id).ToList();
        }

        if (shift!.LineId is { } lineId)
        {
            var lineMachines = await machines.ListByLineAsync(lineId, cancellationToken);
            return lineMachines.Select(m => m.Id).ToList();
        }

        return await ResolveSiteMachinesAsync(scope, shift.SiteId, cancellationToken);
    }

    /// <summary>
    /// Resolves a Story 4.2 Site/Line/Machine report filter against <see cref="CallerScope"/> — the same
    /// spoofable-client-input reasoning as <see cref="Analytics.LossBreakdownQueryUseCase"/>'s
    /// ResolveEquipmentAsync/ResolveAreaAsync, extended with a third (Site) level FR-017 requires that
    /// Epic 3's two-level Equipment/Area filter never needed.
    /// </summary>
    internal async Task<IReadOnlyList<Guid>> ResolveFilterMachinesAsync(
        CallerScope scope, ReportFilterTargetType filterType, Guid filterId, CancellationToken cancellationToken)
    {
        switch (filterType)
        {
            case ReportFilterTargetType.Machine:
            {
                var machine = await machines.GetAsync(filterId, cancellationToken)
                    ?? throw new MasterDataNotFoundException("Machine", filterId);
                var line = await lines.GetAsync(machine.LineId, cancellationToken);
                if (line is null || !scope.AllowsSite(line.SiteId) || !scope.AllowsLine(machine.LineId))
                {
                    throw new MasterDataForbiddenException();
                }

                return [machine.Id];
            }
            case ReportFilterTargetType.Line:
            {
                var line = await lines.GetAsync(filterId, cancellationToken)
                    ?? throw new MasterDataNotFoundException("Line", filterId);
                if (!scope.AllowsSite(line.SiteId) || !scope.AllowsLine(filterId))
                {
                    throw new MasterDataForbiddenException();
                }

                var lineMachines = await machines.ListByLineAsync(filterId, cancellationToken);
                return lineMachines.Select(m => m.Id).ToList();
            }
            case ReportFilterTargetType.Site:
            {
                var site = await sites.GetAsync(filterId, cancellationToken)
                    ?? throw new MasterDataNotFoundException("Site", filterId);
                if (!scope.AllowsSite(site.Id))
                {
                    throw new MasterDataForbiddenException();
                }

                return await ResolveSiteMachinesAsync(scope, site.Id, cancellationToken);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(filterType), filterType, null);
        }
    }

    /// <summary>
    /// Every Machine under every caller-permitted Line of a Site — shared by the Shift-period's
    /// site-wide-shift resolution (Story 4.1) and the Site-level report filter (Story 4.2), instead of
    /// duplicating the composition in two places.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ResolveSiteMachinesAsync(CallerScope scope, Guid siteId, CancellationToken cancellationToken)
    {
        var siteLines = await lines.ListBySiteAsync(siteId, cancellationToken);
        var allowedLines = siteLines.Where(l => scope.AllowsLine(l.Id)).ToList();

        var result = new List<Guid>();
        foreach (var line in allowedLines)
        {
            var lineMachines = await machines.ListByLineAsync(line.Id, cancellationToken);
            result.AddRange(lineMachines.Select(m => m.Id));
        }

        return result;
    }

    private static long SumFor(IReadOnlyList<ClosedDowntimeSlice> slices, LossCategory category, DateTimeOffset start, DateTimeOffset end) =>
        slices.Where(s => s.LossCategory == category).Sum(s => ClippedSeconds(s, start, end));

    /// <summary>
    /// Code-review fix (Epic 4): a slice's raw <see cref="ClosedDowntimeSlice.DurationSeconds"/> can span
    /// past either edge of the report's <c>[start, end)</c> window — <see cref="IDowntimeEventRepository.ListClosedSlicesInRangeAsync"/>
    /// returns any slice that *overlaps* the window, not just ones fully inside it. Clip to the overlapping
    /// portion so a long event doesn't inflate a narrow Shift's loss total beyond its own planned time.
    /// </summary>
    private static long ClippedSeconds(ClosedDowntimeSlice slice, DateTimeOffset start, DateTimeOffset end)
    {
        var clippedStart = slice.StartedAt > start ? slice.StartedAt : start;
        var clippedEnd = slice.EndedAt < end ? slice.EndedAt : end;
        var seconds = (clippedEnd - clippedStart).TotalSeconds;
        return seconds > 0 ? (long)seconds : 0;
    }

    private static double RatioOrZero(double numerator, double denominator) =>
        denominator <= 0 ? 0 : numerator / denominator;
}
