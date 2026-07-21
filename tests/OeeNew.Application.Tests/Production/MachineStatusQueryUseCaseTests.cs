using OeeNew.Application.Auth;
using OeeNew.Application.Production;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Application.Tests.Production;

public class MachineStatusQueryUseCaseTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAsync_GlobalScope_ReturnsAllMachines()
    {
        var machines = new FakeMachineRepository();
        var states = new FakeMachineStateRepository();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var lineA = Guid.NewGuid();
        var lineB = Guid.NewGuid();
        var machineA = machines.Seed("Machine A", lineA, siteA);
        var machineB = machines.Seed("Machine B", lineB, siteB);
        var useCase = new MachineStatusQueryUseCase(machines, states);

        var result = await useCase.ListAsync(CallerScope.Global);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.MachineId == machineA);
        Assert.Contains(result, s => s.MachineId == machineB);
    }

    [Fact]
    public async Task ListAsync_ScopedCaller_ReturnsOnlyInScopeMachines()
    {
        var machines = new FakeMachineRepository();
        var states = new FakeMachineStateRepository();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var lineA = Guid.NewGuid();
        var lineB = Guid.NewGuid();
        var machineA = machines.Seed("Machine A", lineA, siteA);
        machines.Seed("Machine B", lineB, siteB);
        var useCase = new MachineStatusQueryUseCase(machines, states);
        var scope = new CallerScope(false, [siteA], []);

        var result = await useCase.ListAsync(scope);

        var snapshot = Assert.Single(result);
        Assert.Equal(machineA, snapshot.MachineId);
    }

    [Fact]
    public async Task ListAsync_MachineWithNoState_ReturnsNullStatus()
    {
        var machines = new FakeMachineRepository();
        var states = new FakeMachineStateRepository();
        var machineId = machines.Seed("Machine A", Guid.NewGuid());
        var useCase = new MachineStatusQueryUseCase(machines, states);

        var result = await useCase.ListAsync(CallerScope.Global);

        var snapshot = Assert.Single(result);
        Assert.Equal(machineId, snapshot.MachineId);
        Assert.Null(snapshot.Status);
        Assert.Null(snapshot.Counter);
        Assert.Null(snapshot.LastReportedAt);
    }

    [Fact]
    public async Task ListAsync_MachineWithState_ReturnsItsLatestReading()
    {
        var machines = new FakeMachineRepository();
        var states = new FakeMachineStateRepository();
        var machineId = machines.Seed("Machine A", Guid.NewGuid());
        await states.UpsertAsync(new MachineState(machineId, MachineStatus.Running, 7, BaseTime));
        var useCase = new MachineStatusQueryUseCase(machines, states);

        var result = await useCase.ListAsync(CallerScope.Global);

        var snapshot = Assert.Single(result);
        Assert.Equal(MachineStatus.Running, snapshot.Status);
        Assert.Equal(7, snapshot.Counter);
        Assert.Equal(BaseTime, snapshot.LastReportedAt);
    }
}
