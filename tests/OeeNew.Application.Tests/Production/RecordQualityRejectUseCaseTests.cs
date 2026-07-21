using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.Production;

public class RecordQualityRejectUseCaseTests
{
    private sealed record Fixture(RecordQualityRejectUseCase UseCase, FakeMachineRepository Machines, FakeLineRepository Lines, FakeQualityRejectRepository QualityRejects);

    private static Fixture CreateUseCase()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var qualityRejects = new FakeQualityRejectRepository();
        return new Fixture(new RecordQualityRejectUseCase(machines, lines, qualityRejects), machines, lines, qualityRejects);
    }

    [Fact]
    public async Task RecordAsync_HappyPath_PersistsWithRecordedAtSet()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);

        await f.UseCase.RecordAsync(CallerScope.Global, "Operator", machineId, 3);

        var reject = Assert.Single(f.QualityRejects.Added);
        Assert.Equal(machineId, reject.MachineId);
        Assert.Equal(3, reject.Quantity);
        Assert.True(reject.RecordedAt <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Viewer")]
    public async Task RecordAsync_DisallowedRole_ThrowsForbidden(string role)
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            f.UseCase.RecordAsync(CallerScope.Global, role, machineId, 1));
    }

    [Fact]
    public async Task RecordAsync_OperatorScopedToTheMachinesOwnLine_Succeeds()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        var scope = new CallerScope(false, [siteId], [lineId]);

        await f.UseCase.RecordAsync(scope, "Operator", machineId, 2);

        Assert.Single(f.QualityRejects.Added);
    }

    [Fact]
    public async Task RecordAsync_OperatorScopedToDifferentLine_ThrowsForbidden()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);
        var scope = new CallerScope(false, [siteId], [Guid.NewGuid()]);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            f.UseCase.RecordAsync(scope, "Operator", machineId, 1));
    }

    [Fact]
    public async Task RecordAsync_QuantityZeroOrLess_ThrowsValidationError()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId);

        await Assert.ThrowsAsync<MasterDataValidationException>(() =>
            f.UseCase.RecordAsync(CallerScope.Global, "Operator", machineId, 0));
    }

    [Fact]
    public async Task RecordAsync_UnknownMachine_ThrowsParentNotFound()
    {
        var f = CreateUseCase();

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(() =>
            f.UseCase.RecordAsync(CallerScope.Global, "Operator", Guid.NewGuid(), 1));
    }
}
