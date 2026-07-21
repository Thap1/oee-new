using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Application.Tests.Production;

internal sealed record TestReading(Guid MachineId, DateTimeOffset Timestamp, long Counter, MachineStatus Status) : IProductionDataSource;

public class IngestProductionReadingUseCaseTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        IngestProductionReadingUseCase UseCase,
        FakeMachineRepository Machines,
        FakeLineRepository Lines,
        FakeMachineStateRepository States,
        FakeDowntimeEventRepository DowntimeEvents,
        FakeMachineStatusNotifier Notifier);

    private static Fixture CreateUseCase()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var states = new FakeMachineStateRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var notifier = new FakeMachineStatusNotifier();
        return new Fixture(
            new IngestProductionReadingUseCase(machines, lines, states, downtimeEvents, notifier),
            machines, lines, states, downtimeEvents, notifier);
    }

    [Fact]
    public async Task IngestAsync_UnknownMachine_ThrowsParentNotFound()
    {
        var f = CreateUseCase();
        var reading = new TestReading(Guid.NewGuid(), BaseTime, 10, MachineStatus.Running);

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(() =>
            f.UseCase.IngestAsync(CallerScope.Global, "Admin", reading));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Viewer")]
    [InlineData(null)]
    public async Task IngestAsync_DisallowedRole_ThrowsForbidden(string? role)
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        var reading = new TestReading(machineId, BaseTime, 10, MachineStatus.Running);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            f.UseCase.IngestAsync(CallerScope.Global, role, reading));
    }

    [Fact]
    public async Task IngestAsync_OperatorScopedToDifferentLine_ThrowsForbidden()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var otherLineId = f.Lines.Seed("Line B", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        var reading = new TestReading(machineId, BaseTime, 10, MachineStatus.Running);
        var scope = new CallerScope(false, [siteId], [otherLineId]);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            f.UseCase.IngestAsync(scope, "Operator", reading));
    }

    [Fact]
    public async Task IngestAsync_ValidReading_PersistsMachineState()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        var reading = new TestReading(machineId, BaseTime, 42, MachineStatus.Running);

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", reading);

        var state = await f.States.GetAsync(machineId);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Running, state!.Status);
        Assert.Equal(42, state.Counter);
        Assert.Equal(BaseTime, state.LastReportedAt);
    }

    [Fact]
    public async Task IngestAsync_StaleReading_DoesNotOverwritePriorState()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 100, MachineStatus.Running));
        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddSeconds(-5), 999, MachineStatus.Fault));

        var state = await f.States.GetAsync(machineId);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Running, state!.Status);
        Assert.Equal(100, state.Counter);
        Assert.Equal(BaseTime, state.LastReportedAt);
    }

    [Fact]
    public async Task IngestAsync_ScopedOperatorWithinScope_Succeeds()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        var scope = new CallerScope(false, [siteId], [lineId]);

        await f.UseCase.IngestAsync(scope, "Operator", new TestReading(machineId, BaseTime, 5, MachineStatus.Idle));

        var state = await f.States.GetAsync(machineId);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Idle, state!.Status);
    }

    [Fact]
    public async Task IngestAsync_ValidReading_NotifiesMachineStatusChanged()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 42, MachineStatus.Stopped));

        var call = Assert.Single(f.Notifier.Calls);
        Assert.Equal(machineId, call.MachineId);
        Assert.Equal(MachineStatus.Stopped, call.Status);
        Assert.Equal(42, call.Counter);
        Assert.Equal(BaseTime, call.ReportedAt);
    }

    [Fact]
    public async Task IngestAsync_StaleReading_DoesNotNotify()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 100, MachineStatus.Running));
        f.Notifier.Calls.Clear();
        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddSeconds(-5), 999, MachineStatus.Fault));

        Assert.Empty(f.Notifier.Calls);
    }

    [Fact]
    public async Task IngestAsync_TransitionIntoStopped_OpensDowntimeEvent()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 100, MachineStatus.Running));

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddMinutes(1), 100, MachineStatus.Stopped));

        var openEvent = await f.DowntimeEvents.GetOpenByMachineIdAsync(machineId);
        Assert.NotNull(openEvent);
        Assert.Equal(BaseTime.AddMinutes(1), openEvent!.StartedAt);
        Assert.Null(openEvent.ReasonCodeId);
    }

    [Fact]
    public async Task IngestAsync_FirstEverReadingIsStopped_OpensDowntimeEvent()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 0, MachineStatus.Stopped));

        var openEvent = await f.DowntimeEvents.GetOpenByMachineIdAsync(machineId);
        Assert.NotNull(openEvent);
    }

    [Fact]
    public async Task IngestAsync_TransitionOutOfStopped_ClosesTheOpenDowntimeEvent()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 100, MachineStatus.Running));
        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddMinutes(1), 100, MachineStatus.Stopped));

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddMinutes(6), 105, MachineStatus.Running));

        Assert.Null(await f.DowntimeEvents.GetOpenByMachineIdAsync(machineId));
        var closedEvent = Assert.Single(f.DowntimeEvents.All);
        Assert.Equal(BaseTime.AddMinutes(6), closedEvent.EndedAt);
    }

    [Fact]
    public async Task IngestAsync_TransitionBetweenNonStoppedStatuses_TouchesNoDowntimeEvent()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 100, MachineStatus.Running));

        await f.UseCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddMinutes(1), 100, MachineStatus.Idle));

        Assert.Empty(f.DowntimeEvents.All);
    }
}
