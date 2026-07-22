using System.Globalization;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.Reports;

/// <summary>
/// Aggregated OEE report over a Shift/Day/Week period (Story 4.1, FR-016). Computes a
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
/// <see cref="ResolvePeriodAsync"/>/<see cref="ResolveMachinesAsync"/> are split out as reusable building
/// blocks — Story 4.3's top-downtime-reason report calls back into this same class rather than duplicating them.
/// </summary>
public sealed class OeeReportQueryUseCase(
    IMachineRepository machines,
    ILineRepository lines,
    IShiftScheduleRepository shiftSchedules,
    IDowntimeEventRepository downtimeEvents,
    IQualityRejectRepository qualityRejects)
{
    public async Task<OeeReportResult> GetReportAsync(
        CallerScope scope, ReportPeriodType periodType, DateOnly referenceDate, Guid? shiftScheduleId, CancellationToken cancellationToken = default)
    {
        var (start, end, plannedSeconds) = await ResolvePeriodAsync(scope, periodType, referenceDate, shiftScheduleId, cancellationToken);
        var machineIds = await ResolveMachinesAsync(scope, periodType, referenceDate, shiftScheduleId, cancellationToken);

        if (machineIds.Count == 0)
        {
            return new OeeReportResult(periodType, start, end, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var slices = await downtimeEvents.ListClosedSlicesInRangeAsync(machineIds, start, end, cancellationToken);
        var availabilityLoss = SumFor(slices, LossCategory.AvailabilityLoss);
        var performanceLoss = SumFor(slices, LossCategory.PerformanceLoss);
        var qualityLoss = SumFor(slices, LossCategory.QualityLoss);
        var unattributed = slices.Where(s => s.LossCategory is null).Sum(s => s.DurationSeconds);

        var rejectQuantity = await qualityRejects.SumQuantityInRangeAsync(machineIds, start, end, cancellationToken);

        var afterAvailability = plannedSeconds - availabilityLoss;
        var afterPerformance = afterAvailability - performanceLoss;
        var afterQuality = afterPerformance - qualityLoss;

        var availabilityPercent = RatioOrZero(afterAvailability, plannedSeconds);
        var performancePercent = RatioOrZero(afterPerformance, afterAvailability);
        var qualityPercent = RatioOrZero(afterQuality, afterPerformance);
        var oeePercent = availabilityPercent * performancePercent * qualityPercent;

        return new OeeReportResult(
            periodType, start, end,
            availabilityPercent, performancePercent, qualityPercent, oeePercent,
            availabilityLoss, performanceLoss, qualityLoss, unattributed, rejectQuantity);
    }

    /// <summary>
    /// Resolves the period's absolute <c>[start, end)</c> instant window and its planned-time denominator.
    /// Day/Week use the full UTC calendar period (no per-site "operating hours" concept exists in master
    /// data); Shift combines the seeded <see cref="ShiftSchedule"/>'s time-of-day window with
    /// <paramref name="referenceDate"/>, handling the overnight-wrap case the same way
    /// <see cref="ShiftSchedule.OverlapsWith"/> does.
    /// </summary>
    internal async Task<(DateTimeOffset Start, DateTimeOffset End, long PlannedSeconds)> ResolvePeriodAsync(
        CallerScope scope, ReportPeriodType periodType, DateOnly referenceDate, Guid? shiftScheduleId, CancellationToken cancellationToken)
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
                var shift = await shiftSchedules.GetAsync(shiftScheduleId!.Value, cancellationToken)
                    ?? throw new MasterDataNotFoundException("ShiftSchedule", shiftScheduleId.Value);
                if (!scope.AllowsSite(shift.SiteId) || (shift.LineId is { } lineId && !scope.AllowsLine(lineId)))
                {
                    throw new MasterDataForbiddenException();
                }

                var referenceMidnight = DateTime.SpecifyKind(referenceDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
                var start = new DateTimeOffset(referenceMidnight, TimeSpan.Zero) + shift.StartTime.ToTimeSpan();
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
    /// Resolves every Machine the report should aggregate over. Day/Week fall back to the caller's full
    /// scope (Story 4.2 later narrows this with explicit site/line/machine filters). Shift resolves through
    /// the picked <see cref="ShiftSchedule"/>'s Site/Line — treated as spoofable client input and checked
    /// against <see cref="CallerScope"/>, the same reasoning
    /// <see cref="Analytics.LossBreakdownQueryUseCase"/>'s ResolveEquipmentAsync/ResolveAreaAsync use.
    /// </summary>
    internal async Task<IReadOnlyList<Guid>> ResolveMachinesAsync(
        CallerScope scope, ReportPeriodType periodType, DateOnly referenceDate, Guid? shiftScheduleId, CancellationToken cancellationToken)
    {
        if (periodType != ReportPeriodType.Shift)
        {
            var scoped = await machines.ListByScopeAsync(scope, cancellationToken);
            return scoped.Select(m => m.Id).ToList();
        }

        var shift = await shiftSchedules.GetAsync(shiftScheduleId!.Value, cancellationToken)
            ?? throw new MasterDataNotFoundException("ShiftSchedule", shiftScheduleId.Value);
        if (!scope.AllowsSite(shift.SiteId))
        {
            throw new MasterDataForbiddenException();
        }

        if (shift.LineId is { } lineId)
        {
            if (!scope.AllowsLine(lineId))
            {
                throw new MasterDataForbiddenException();
            }

            var lineMachines = await machines.ListByLineAsync(lineId, cancellationToken);
            return lineMachines.Select(m => m.Id).ToList();
        }

        var siteLines = await lines.ListBySiteAsync(shift.SiteId, cancellationToken);
        var allowedLines = siteLines.Where(l => scope.AllowsLine(l.Id)).ToList();

        var result = new List<Guid>();
        foreach (var line in allowedLines)
        {
            var lineMachines = await machines.ListByLineAsync(line.Id, cancellationToken);
            result.AddRange(lineMachines.Select(m => m.Id));
        }

        return result;
    }

    private static long SumFor(IReadOnlyList<ClosedDowntimeSlice> slices, LossCategory category) =>
        slices.Where(s => s.LossCategory == category).Sum(s => s.DurationSeconds);

    private static double RatioOrZero(double numerator, double denominator) =>
        denominator <= 0 ? 0 : numerator / denominator;
}
