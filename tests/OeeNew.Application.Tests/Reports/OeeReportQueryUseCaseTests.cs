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
        FakeDowntimeEventRepository downtimeEvents, FakeQualityRejectRepository qualityRejects) =>
        new(machines, lines, shifts, downtimeEvents, qualityRejects);

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
