using OeeNew.Application.Auth;
using OeeNew.Application.Production;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Application.Tests.Production;

public class DowntimeHistoryQueryUseCaseTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 24, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAsync_NoMachinesInScope_ReturnsEmpty()
    {
        var useCase = new DowntimeHistoryQueryUseCase(new FakeMachineRepository(), new FakeDowntimeEventRepository(), new FakeReasonCodeRepository());

        var result = await useCase.ListAsync(CallerScope.Global);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_ClosedEventWithReason_ResolvesMachineAndReasonNamesAndDuration()
    {
        var machines = new FakeMachineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var machineId = machines.Seed("Máy ép nhựa 01", Guid.NewGuid(), siteId);
        var reasonCodeId = reasonCodes.Seed(siteId, "Hỏng máy", LossCategory.AvailabilityLoss);

        var closedEvent = new DowntimeEvent(Guid.NewGuid(), machineId, BaseTime);
        closedEvent.AssignReason(reasonCodeId);
        closedEvent.Close(BaseTime.AddMinutes(5));
        downtimeEvents.SeedEvent(closedEvent);

        var useCase = new DowntimeHistoryQueryUseCase(machines, downtimeEvents, reasonCodes);

        var result = await useCase.ListAsync(CallerScope.Global);

        var entry = Assert.Single(result);
        Assert.Equal("Máy ép nhựa 01", entry.MachineName);
        Assert.Equal("Hỏng máy", entry.ReasonCodeName);
        Assert.Equal(300, entry.DurationSeconds);
        Assert.NotNull(entry.EndedAt);
    }

    [Fact]
    public async Task ListAsync_OpenEventWithoutReason_ReturnsNullEndedAtAndDuration()
    {
        var machines = new FakeMachineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteId = Guid.NewGuid();
        var machineId = machines.Seed("Máy đóng gói 01", Guid.NewGuid(), siteId);
        downtimeEvents.SeedEvent(new DowntimeEvent(Guid.NewGuid(), machineId, BaseTime));

        var useCase = new DowntimeHistoryQueryUseCase(machines, downtimeEvents, reasonCodes);

        var result = await useCase.ListAsync(CallerScope.Global);

        var entry = Assert.Single(result);
        Assert.Null(entry.ReasonCodeId);
        Assert.Null(entry.ReasonCodeName);
        Assert.Null(entry.EndedAt);
        Assert.Null(entry.DurationSeconds);
    }

    [Fact]
    public async Task ListAsync_ScopedCaller_ExcludesOutOfScopeMachineEvents()
    {
        var machines = new FakeMachineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var machineA = machines.Seed("Machine A", Guid.NewGuid(), siteA);
        var machineB = machines.Seed("Machine B", Guid.NewGuid(), siteB);
        downtimeEvents.SeedEvent(new DowntimeEvent(Guid.NewGuid(), machineA, BaseTime));
        downtimeEvents.SeedEvent(new DowntimeEvent(Guid.NewGuid(), machineB, BaseTime));
        var useCase = new DowntimeHistoryQueryUseCase(machines, downtimeEvents, reasonCodes);
        var scope = new CallerScope(false, [siteA], []);

        var result = await useCase.ListAsync(scope);

        var entry = Assert.Single(result);
        Assert.Equal(machineA, entry.MachineId);
    }

    [Fact]
    public async Task ListAsync_MultipleEvents_OrderedNewestFirst()
    {
        var machines = new FakeMachineRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var machineId = machines.Seed("Machine A", Guid.NewGuid(), Guid.NewGuid());
        var older = new DowntimeEvent(Guid.NewGuid(), machineId, BaseTime);
        older.Close(BaseTime.AddMinutes(1));
        var newer = new DowntimeEvent(Guid.NewGuid(), machineId, BaseTime.AddHours(1));
        newer.Close(BaseTime.AddHours(1).AddMinutes(1));
        downtimeEvents.SeedEvent(older);
        downtimeEvents.SeedEvent(newer);
        var useCase = new DowntimeHistoryQueryUseCase(machines, downtimeEvents, reasonCodes);

        var result = await useCase.ListAsync(CallerScope.Global);

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].Id);
        Assert.Equal(older.Id, result[1].Id);
    }
}
