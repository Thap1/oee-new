using OeeNew.Application.Analytics;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Application.Tests.Production;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Application.Tests.Analytics;

public class LossBreakdownQueryUseCaseTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_Equipment_SumsSecondsPerLossCategory()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);

        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 100, BaseTime);
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 50, BaseTime);
        downtimeEvents.SeedClosed(machineId, LossCategory.PerformanceLoss, 30, BaseTime);
        downtimeEvents.SeedClosed(machineId, LossCategory.QualityLoss, 20, BaseTime);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);
        var result = await useCase.GetAsync(CallerScope.Global, LossBreakdownTargetType.Equipment, machineId, date: null);

        Assert.Equal(150, result.AvailabilitySeconds);
        Assert.Equal(30, result.PerformanceSeconds);
        Assert.Equal(20, result.QualitySeconds);
    }

    [Fact]
    public async Task GetAsync_ClosedEventWithNoReasonCode_CountsAsUnattributed_NotAnyCategory()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);

        downtimeEvents.SeedClosed(machineId, lossCategory: null, durationSeconds: 40, BaseTime);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);
        var result = await useCase.GetAsync(CallerScope.Global, LossBreakdownTargetType.Equipment, machineId, date: null);

        Assert.Equal(40, result.UnattributedSeconds);
        Assert.Equal(0, result.AvailabilitySeconds);
        Assert.Equal(0, result.PerformanceSeconds);
        Assert.Equal(0, result.QualitySeconds);
    }

    [Fact]
    public async Task GetAsync_Area_AggregatesAcrossEveryMachineOnTheLine()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineA = machines.Seed("Machine A", lineId, siteId);
        var machineB = machines.Seed("Machine B", lineId, siteId);

        downtimeEvents.SeedClosed(machineA, LossCategory.AvailabilityLoss, 60, BaseTime);
        downtimeEvents.SeedClosed(machineB, LossCategory.AvailabilityLoss, 40, BaseTime);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);
        var result = await useCase.GetAsync(CallerScope.Global, LossBreakdownTargetType.Area, lineId, date: null);

        Assert.Equal(100, result.AvailabilitySeconds);
    }

    [Fact]
    public async Task GetAsync_QualityRejectQuantity_IsSupplementary_NotBlendedIntoQualitySeconds()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);

        downtimeEvents.SeedClosed(machineId, LossCategory.QualityLoss, 20, BaseTime);
        await qualityRejects.AddAsync(new QualityReject(Guid.Empty, machineId, 5, BaseTime));

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);
        var result = await useCase.GetAsync(CallerScope.Global, LossBreakdownTargetType.Equipment, machineId, date: null);

        Assert.Equal(20, result.QualitySeconds);
        Assert.Equal(5, result.QualityRejectQuantity);
    }

    [Fact]
    public async Task GetAsync_ScopedCallerOutsideTargetScope_ThrowsForbidden()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var lineA = lines.Seed("Line A", siteA);
        var machineInA = machines.Seed("Machine A", lineA, siteA);
        var scopedToSiteB = new CallerScope(false, [siteB], []);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            useCase.GetAsync(scopedToSiteB, LossBreakdownTargetType.Equipment, machineInA, date: null));
    }

    [Fact]
    public async Task GetAsync_NonexistentEquipmentTargetId_ThrowsNotFound()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() =>
            useCase.GetAsync(CallerScope.Global, LossBreakdownTargetType.Equipment, Guid.NewGuid(), date: null));
    }

    [Fact]
    public async Task GetAsync_NonexistentAreaTargetId_ThrowsNotFound()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() =>
            useCase.GetAsync(CallerScope.Global, LossBreakdownTargetType.Area, Guid.NewGuid(), date: null));
    }

    [Fact]
    public async Task GetAsync_WithDate_OnlyCountsEventsOnThatUtcCalendarDay()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var day1 = new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 7, 21, 9, 0, 0, TimeSpan.Zero);

        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 100, day1);
        downtimeEvents.SeedClosed(machineId, LossCategory.AvailabilityLoss, 999, day2);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);
        var result = await useCase.GetAsync(CallerScope.Global, LossBreakdownTargetType.Equipment, machineId, date: DateOnly.FromDateTime(day1.UtcDateTime));

        Assert.Equal(100, result.AvailabilitySeconds);
    }

    [Fact]
    public async Task GetReasonBreakdownAsync_GroupsByReasonCode_SummedAndOrderedByDurationDescending()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var reasonA = reasonCodes.Seed(siteId, "Kẹt khuôn", LossCategory.AvailabilityLoss);
        var reasonB = reasonCodes.Seed(siteId, "Đổi ca", LossCategory.AvailabilityLoss);

        downtimeEvents.SeedClosed(machineId, reasonA, LossCategory.AvailabilityLoss, 30, BaseTime);
        downtimeEvents.SeedClosed(machineId, reasonA, LossCategory.AvailabilityLoss, 20, BaseTime);
        downtimeEvents.SeedClosed(machineId, reasonB, LossCategory.AvailabilityLoss, 10, BaseTime);
        // A PerformanceLoss event on the same machine must not leak into the AvailabilityLoss drill-down.
        var reasonC = reasonCodes.Seed(siteId, "Đổi khuôn", LossCategory.PerformanceLoss);
        downtimeEvents.SeedClosed(machineId, reasonC, LossCategory.PerformanceLoss, 999, BaseTime);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);
        var result = await useCase.GetReasonBreakdownAsync(
            CallerScope.Global, LossBreakdownTargetType.Equipment, machineId, LossCategory.AvailabilityLoss, date: null);

        Assert.Equal(2, result.Count);
        Assert.Equal(reasonA, result[0].ReasonCodeId);
        Assert.Equal(50, result[0].DurationSeconds);
        Assert.Equal(reasonB, result[1].ReasonCodeId);
        Assert.Equal(10, result[1].DurationSeconds);
    }

    [Fact]
    public async Task GetReasonBreakdownAsync_NoMatchingEvents_ReturnsEmpty()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId, siteId);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);
        var result = await useCase.GetReasonBreakdownAsync(
            CallerScope.Global, LossBreakdownTargetType.Equipment, machineId, LossCategory.QualityLoss, date: null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAsync_ScopedCallerOutsideAreaScope_ThrowsForbidden()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var lineA = lines.Seed("Line A", siteA);
        var scopedToSiteB = new CallerScope(false, [siteB], []);

        var useCase = new LossBreakdownQueryUseCase(machines, lines, downtimeEvents, qualityRejects, reasonCodes);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            useCase.GetAsync(scopedToSiteB, LossBreakdownTargetType.Area, lineA, date: null));
    }
}
