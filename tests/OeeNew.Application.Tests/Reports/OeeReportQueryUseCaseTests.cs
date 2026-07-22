using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Reports;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Application.Tests.Production;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.Reports;

public class OeeReportQueryUseCaseTests
{
    private static OeeReportQueryUseCase BuildUseCase(
        FakeMachineRepository machines, FakeLineRepository lines, FakeShiftScheduleRepository shifts,
        FakeDowntimeEventRepository downtimeEvents, FakeQualityRejectRepository qualityRejects,
        FakeSiteRepository? sites = null, FakeReasonCodeRepository? reasonCodes = null) =>
        new(sites ?? new FakeSiteRepository(), machines, lines, shifts, downtimeEvents, qualityRejects, reasonCodes ?? new FakeReasonCodeRepository());

    [Fact]
    public async Task GetReportAsync_Day_UsesExactUtcCalendarDayBoundaries()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);

        var referenceDate = new DateOnly(2026, 7, 20);
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 100, new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero));
        // Just before the day starts and just at/after it ends — must be excluded.
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 999, new DateTimeOffset(2026, 7, 19, 23, 59, 59, TimeSpan.Zero));
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 999, new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null);

        Assert.Equal(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), result.PeriodStart);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero), result.PeriodEnd);
        Assert.Equal(100, result.AvailabilityLossSeconds);
    }

    [Fact]
    public async Task GetReportAsync_Week_ResolvesIsoMondayToMondayContainingReferenceDate()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();

        // 2026-07-20 is a Monday; pick a mid-week Thursday to confirm it resolves back to that Monday.
        var referenceDate = new DateOnly(2026, 7, 23);

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Week, referenceDate, shiftScheduleId: null);

        Assert.Equal(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), result.PeriodStart);
        Assert.Equal(new DateTimeOffset(2026, 7, 27, 0, 0, 0, TimeSpan.Zero), result.PeriodEnd);
    }

    [Fact]
    public async Task GetReportAsync_Shift_OvernightWrap_ResolvesWindowAcrossMidnight()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var shiftId = shifts.Seed(siteId, lineId, "Night", new TimeOnly(22, 0), new TimeOnly(6, 0));

        var referenceDate = new DateOnly(2026, 7, 20);
        // Inside the window (23:00 on referenceDate).
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 100, new DateTimeOffset(2026, 7, 20, 23, 0, 0, TimeSpan.Zero));
        // Outside the window (07:00 the next day, after the 06:00 end).
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 999, new DateTimeOffset(2026, 7, 21, 7, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Shift, referenceDate, shiftId);

        Assert.Equal(new DateTimeOffset(2026, 7, 20, 22, 0, 0, TimeSpan.Zero), result.PeriodStart);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 6, 0, 0, TimeSpan.Zero), result.PeriodEnd);
        Assert.Equal(100, result.AvailabilityLossSeconds);
    }

    [Fact]
    public async Task GetReportAsync_FullAvailabilityLoss_PerformanceAndQualityAreZero_NotNaN()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);

        var referenceDate = new DateOnly(2026, 7, 20);
        // 100% of the day lost to Availability.
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 86_400, new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null);

        Assert.Equal(0, result.AvailabilityPercent);
        Assert.Equal(0, result.PerformancePercent);
        Assert.Equal(0, result.QualityPercent);
        Assert.Equal(0, result.OeePercent);
        Assert.False(double.IsNaN(result.PerformancePercent));
        Assert.False(double.IsNaN(result.QualityPercent));
    }

    [Fact]
    public async Task GetReportAsync_UnattributedSeconds_ExcludedFromAllThreeSums_SurfacedSeparately()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);

        var referenceDate = new DateOnly(2026, 7, 20);
        downtimeEvents.SeedClosed(machineId, lossCategory: null, durationSeconds: 500, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null);

        Assert.Equal(500, result.UnattributedSeconds);
        Assert.Equal(0, result.AvailabilityLossSeconds);
        Assert.Equal(0, result.PerformanceLossSeconds);
        Assert.Equal(0, result.QualityLossSeconds);
        Assert.Equal(1.0, result.AvailabilityPercent, 5);
    }

    [Fact]
    public async Task GetReportAsync_ShiftOutsideCallerScope_ThrowsForbidden()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteA = Guid.NewGuid();
        var lineA = lines.Seed("Line A", siteA);
        var shiftId = shifts.Seed(siteA, lineA, "Day", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var scopedToOtherSite = new CallerScope(false, [Guid.NewGuid()], []);

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            useCase.GetReportAsync(scopedToOtherSite, ReportPeriodType.Shift, new DateOnly(2026, 7, 20), shiftId));
    }

    [Fact]
    public async Task GetReportAsync_MachineFilter_NarrowsToJustThatMachinesDowntime()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineA = machines.Seed("Machine A", lineId, siteId);
        var machineB = machines.Seed("Machine B", lineId, siteId);

        var referenceDate = new DateOnly(2026, 7, 20);
        downtimeEvents.SeedClosed(machineA, LossCategory.AvailabilityLoss, 100, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));
        downtimeEvents.SeedClosed(machineB, LossCategory.AvailabilityLoss, 999, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(
            CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null, ReportFilterTargetType.Machine, machineA);

        Assert.Equal(100, result.AvailabilityLossSeconds);
    }

    [Fact]
    public async Task GetReportAsync_SiteFilter_ComposesAcrossMultipleLinesOfThatSite()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var lineA = lines.Seed("Line A", siteId);
        var lineB = lines.Seed("Line B", siteId);
        var machineA = machines.Seed("Machine A", lineA, siteId);
        var machineB = machines.Seed("Machine B", lineB, siteId);

        var referenceDate = new DateOnly(2026, 7, 20);
        downtimeEvents.SeedClosed(machineA, LossCategory.AvailabilityLoss, 60, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));
        downtimeEvents.SeedClosed(machineB, LossCategory.AvailabilityLoss, 40, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects, sites);
        var result = await useCase.GetReportAsync(
            CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null, ReportFilterTargetType.Site, siteId);

        Assert.Equal(100, result.AvailabilityLossSeconds);
    }

    [Theory]
    [InlineData(ReportFilterTargetType.Site)]
    [InlineData(ReportFilterTargetType.Line)]
    [InlineData(ReportFilterTargetType.Machine)]
    public async Task GetReportAsync_FilterOutsideCallerScope_ThrowsForbidden(ReportFilterTargetType filterType)
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var scopedToOtherSite = new CallerScope(false, [Guid.NewGuid()], []);

        var filterId = filterType switch
        {
            ReportFilterTargetType.Site => siteId,
            ReportFilterTargetType.Line => lineId,
            _ => machineId,
        };

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects, sites);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            useCase.GetReportAsync(scopedToOtherSite, ReportPeriodType.Day, new DateOnly(2026, 7, 20), shiftScheduleId: null, filterType, filterId));
    }

    [Fact]
    public async Task GetReportAsync_ValidFilterDisjointFromPeriodImpliedScope_ReturnsAllZero_NotException()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var shiftLine = lines.Seed("Shift Line", siteId);
        var otherLine = lines.Seed("Other Line", siteId);
        machines.Seed("Machine On Shift Line", shiftLine, siteId);
        var otherMachine = machines.Seed("Machine On Other Line", otherLine, siteId);
        var shiftId = shifts.Seed(siteId, shiftLine, "Day", new TimeOnly(8, 0), new TimeOnly(16, 0));

        downtimeEvents.SeedClosed(otherMachine, LossCategory.AvailabilityLoss, 500, new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        // Filter to Other Line, which is a legitimate in-scope Line but not part of the picked Shift's Line.
        var result = await useCase.GetReportAsync(
            CallerScope.Global, ReportPeriodType.Shift, new DateOnly(2026, 7, 20), shiftId, ReportFilterTargetType.Line, otherLine);

        Assert.Equal(0, result.AvailabilityLossSeconds);
        Assert.Equal(0, result.OeePercent);
    }

    [Fact]
    public async Task GetReportAsync_TopDowntimeReason_TieBreaksByNameOrdinal_StableAcrossRepeatedCalls()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var reasonZ = reasonCodes.Seed(siteId, "Zeta", LossCategory.AvailabilityLoss);
        var reasonA = reasonCodes.Seed(siteId, "Alpha", LossCategory.AvailabilityLoss);

        var referenceDate = new DateOnly(2026, 7, 20);
        downtimeEvents.SeedClosed(machineId, reasonZ, LossCategory.AvailabilityLoss, 50, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));
        downtimeEvents.SeedClosed(machineId, reasonA, LossCategory.AvailabilityLoss, 50, new DateTimeOffset(2026, 7, 20, 2, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects, reasonCodes: reasonCodes);
        var result1 = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null);
        var result2 = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null);

        Assert.Equal(reasonA, result1.TopDowntimeReasonCodeId);
        Assert.Equal("Alpha", result1.TopDowntimeReasonName);
        Assert.Equal(50, result1.TopDowntimeReasonSeconds);
        Assert.Equal(result1.TopDowntimeReasonCodeId, result2.TopDowntimeReasonCodeId);
    }

    [Fact]
    public async Task GetReportAsync_TopDowntimeReason_NotFilteredByLossCategory()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var performanceReason = reasonCodes.Seed(siteId, "Changeover", LossCategory.PerformanceLoss);
        var availabilityReason = reasonCodes.Seed(siteId, "Breakdown", LossCategory.AvailabilityLoss);

        var referenceDate = new DateOnly(2026, 7, 20);
        downtimeEvents.SeedClosed(machineId, performanceReason, LossCategory.PerformanceLoss, 200, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));
        downtimeEvents.SeedClosed(machineId, availabilityReason, LossCategory.AvailabilityLoss, 50, new DateTimeOffset(2026, 7, 20, 2, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects, reasonCodes: reasonCodes);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, referenceDate, shiftScheduleId: null);

        Assert.Equal(performanceReason, result.TopDowntimeReasonCodeId);
        Assert.Equal(200, result.TopDowntimeReasonSeconds);
    }

    [Fact]
    public async Task GetReportAsync_NoClosedEvents_TopDowntimeReasonFieldsAllNull()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        machines.Seed("Machine A", lineId, siteId);

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, new DateOnly(2026, 7, 20), shiftScheduleId: null);

        Assert.Null(result.TopDowntimeReasonCodeId);
        Assert.Null(result.TopDowntimeReasonName);
        Assert.Null(result.TopDowntimeReasonSeconds);
    }

    [Fact]
    public async Task GetReportAsync_OnlyUnattributedEvents_TopDowntimeReasonFieldsAllNull()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        downtimeEvents.SeedClosed(machineId, lossCategory: null, durationSeconds: 300, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, new DateOnly(2026, 7, 20), shiftScheduleId: null);

        Assert.Null(result.TopDowntimeReasonCodeId);
        Assert.Null(result.TopDowntimeReasonName);
        Assert.Null(result.TopDowntimeReasonSeconds);
    }

    [Fact]
    public async Task GetReportAsync_TopDowntimeReason_DoesNotPerturbExistingPercentages()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var reason = reasonCodes.Seed(siteId, "Breakdown", LossCategory.AvailabilityLoss);
        downtimeEvents.SeedClosed(machineId, reason, LossCategory.AvailabilityLoss, 3600, new DateTimeOffset(2026, 7, 20, 1, 0, 0, TimeSpan.Zero));

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects, reasonCodes: reasonCodes);
        var result = await useCase.GetReportAsync(CallerScope.Global, ReportPeriodType.Day, new DateOnly(2026, 7, 20), shiftScheduleId: null);

        Assert.Equal(reason, result.TopDowntimeReasonCodeId);
        Assert.Equal(3600, result.TopDowntimeReasonSeconds);
        Assert.Equal(3600, result.AvailabilityLossSeconds);
        Assert.True(result.AvailabilityPercent < 1.0 && result.AvailabilityPercent > 0.9);
    }

    [Fact]
    public async Task GetReportAsync_EmptyScope_ReturnsAllZeroResult_NotException()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var shifts = new FakeShiftScheduleRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var emptyScope = new CallerScope(false, [], []);

        var useCase = BuildUseCase(machines, lines, shifts, downtimeEvents, qualityRejects);
        var result = await useCase.GetReportAsync(emptyScope, ReportPeriodType.Day, new DateOnly(2026, 7, 20), shiftScheduleId: null);

        Assert.Equal(0, result.AvailabilityPercent);
        Assert.Equal(0, result.PerformancePercent);
        Assert.Equal(0, result.QualityPercent);
        Assert.Equal(0, result.OeePercent);
        Assert.Equal(0, result.QualityRejectQuantity);
    }
}
