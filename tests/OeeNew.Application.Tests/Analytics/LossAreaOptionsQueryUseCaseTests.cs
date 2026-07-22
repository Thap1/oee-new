using OeeNew.Application.Analytics;
using OeeNew.Application.Auth;
using OeeNew.Application.Tests.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.Analytics;

public class LossAreaOptionsQueryUseCaseTests
{
    [Fact]
    public async Task ListAsync_ScopedCaller_ReturnsOnlyLinesWithAMachineInScope()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var lineA = lines.Seed("Line A", siteA);
        var lineB = lines.Seed("Line B", siteB);
        // Line C exists but has no Machine at all under it — and separately, is outside the caller's scope.
        var lineC = lines.Seed("Line C", siteB);
        machines.Seed("Machine A", lineA, siteA);
        machines.Seed("Machine B", lineB, siteB);
        var scope = new CallerScope(false, [siteA], []);

        var useCase = new LossAreaOptionsQueryUseCase(machines, lines);
        var result = await useCase.ListAsync(scope);

        var area = Assert.Single(result);
        Assert.Equal(lineA, area.LineId);
        Assert.DoesNotContain(result, a => a.LineId == lineB);
        Assert.DoesNotContain(result, a => a.LineId == lineC);
    }

    [Fact]
    public async Task ListAsync_CallerWithNoAssignedMachines_ReturnsEmpty()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var siteId = Guid.NewGuid();
        lines.Seed("Line A", siteId);
        var scope = new CallerScope(false, [siteId], []);

        var useCase = new LossAreaOptionsQueryUseCase(machines, lines);
        var result = await useCase.ListAsync(scope);

        Assert.Empty(result);
    }
}
