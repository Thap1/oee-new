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

    private static (IngestProductionReadingUseCase UseCase, FakeMachineRepository Machines, FakeLineRepository Lines, FakeMachineStateRepository States, FakeMachineStatusNotifier Notifier) CreateUseCase()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var states = new FakeMachineStateRepository();
        var notifier = new FakeMachineStatusNotifier();
        return (new IngestProductionReadingUseCase(machines, lines, states, notifier), machines, lines, states, notifier);
    }

    [Fact]
    public async Task IngestAsync_UnknownMachine_ThrowsParentNotFound()
    {
        var (useCase, _, _, _, _) = CreateUseCase();
        var reading = new TestReading(Guid.NewGuid(), BaseTime, 10, MachineStatus.Running);

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(() =>
            useCase.IngestAsync(CallerScope.Global, "Admin", reading));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Viewer")]
    [InlineData(null)]
    public async Task IngestAsync_DisallowedRole_ThrowsForbidden(string? role)
    {
        var (useCase, machines, lines, _, _) = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId);
        var reading = new TestReading(machineId, BaseTime, 10, MachineStatus.Running);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            useCase.IngestAsync(CallerScope.Global, role, reading));
    }

    [Fact]
    public async Task IngestAsync_OperatorScopedToDifferentLine_ThrowsForbidden()
    {
        var (useCase, machines, lines, _, _) = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var otherLineId = lines.Seed("Line B", siteId);
        var machineId = machines.Seed("Machine A", lineId);
        var reading = new TestReading(machineId, BaseTime, 10, MachineStatus.Running);
        var scope = new CallerScope(false, [siteId], [otherLineId]);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            useCase.IngestAsync(scope, "Operator", reading));
    }

    [Fact]
    public async Task IngestAsync_ValidReading_PersistsMachineState()
    {
        var (useCase, machines, lines, states, _) = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId);
        var reading = new TestReading(machineId, BaseTime, 42, MachineStatus.Running);

        await useCase.IngestAsync(CallerScope.Global, "Operator", reading);

        var state = await states.GetAsync(machineId);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Running, state!.Status);
        Assert.Equal(42, state.Counter);
        Assert.Equal(BaseTime, state.LastReportedAt);
    }

    [Fact]
    public async Task IngestAsync_StaleReading_DoesNotOverwritePriorState()
    {
        var (useCase, machines, lines, states, _) = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId);

        await useCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 100, MachineStatus.Running));
        await useCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddSeconds(-5), 999, MachineStatus.Fault));

        var state = await states.GetAsync(machineId);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Running, state!.Status);
        Assert.Equal(100, state.Counter);
        Assert.Equal(BaseTime, state.LastReportedAt);
    }

    [Fact]
    public async Task IngestAsync_ScopedOperatorWithinScope_Succeeds()
    {
        var (useCase, machines, lines, states, _) = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId);
        var scope = new CallerScope(false, [siteId], [lineId]);

        await useCase.IngestAsync(scope, "Operator", new TestReading(machineId, BaseTime, 5, MachineStatus.Idle));

        var state = await states.GetAsync(machineId);
        Assert.NotNull(state);
        Assert.Equal(MachineStatus.Idle, state!.Status);
    }

    [Fact]
    public async Task IngestAsync_ValidReading_NotifiesMachineStatusChanged()
    {
        var (useCase, machines, lines, _, notifier) = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId);

        await useCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 42, MachineStatus.Stopped));

        var call = Assert.Single(notifier.Calls);
        Assert.Equal(machineId, call.MachineId);
        Assert.Equal(MachineStatus.Stopped, call.Status);
        Assert.Equal(42, call.Counter);
        Assert.Equal(BaseTime, call.ReportedAt);
    }

    [Fact]
    public async Task IngestAsync_StaleReading_DoesNotNotify()
    {
        var (useCase, machines, lines, _, notifier) = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = lines.Seed("Line A", siteId);
        var machineId = machines.Seed("Machine A", lineId);

        await useCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime, 100, MachineStatus.Running));
        notifier.Calls.Clear();
        await useCase.IngestAsync(CallerScope.Global, "Operator", new TestReading(machineId, BaseTime.AddSeconds(-5), 999, MachineStatus.Fault));

        Assert.Empty(notifier.Calls);
    }
}
