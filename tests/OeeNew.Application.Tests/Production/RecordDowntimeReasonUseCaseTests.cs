using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Application.Tests.Production;

public class RecordDowntimeReasonUseCaseTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        RecordDowntimeReasonUseCase UseCase,
        FakeMachineRepository Machines,
        FakeLineRepository Lines,
        FakeReasonCodeRepository ReasonCodes,
        FakeDowntimeEventRepository DowntimeEvents,
        FakeMachineStatusNotifier Notifier);

    private static Fixture CreateUseCase()
    {
        var machines = new FakeMachineRepository();
        var lines = new FakeLineRepository();
        var reasonCodes = new FakeReasonCodeRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var notifier = new FakeMachineStatusNotifier();
        return new Fixture(
            new RecordDowntimeReasonUseCase(machines, lines, reasonCodes, downtimeEvents, notifier),
            machines, lines, reasonCodes, downtimeEvents, notifier);
    }

    private static async Task<(Guid MachineId, Guid SiteId, Guid LineId)> SeedMachineWithOpenDowntimeAsync(Fixture f)
    {
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId, siteId);
        await f.DowntimeEvents.AddAsync(new DowntimeEvent(Guid.Empty, machineId, BaseTime));
        return (machineId, siteId, lineId);
    }

    [Fact]
    public async Task AttachReasonAsync_HappyPath_AssignsReasonAndNotifies()
    {
        var f = CreateUseCase();
        var (machineId, siteId, _) = await SeedMachineWithOpenDowntimeAsync(f);
        var reasonCodeId = f.ReasonCodes.Seed(siteId, "Jam", LossCategory.AvailabilityLoss);

        await f.UseCase.AttachReasonAsync(CallerScope.Global, "Operator", machineId, reasonCodeId);

        var openEvent = await f.DowntimeEvents.GetOpenByMachineIdAsync(machineId);
        Assert.NotNull(openEvent);
        Assert.Equal(reasonCodeId, openEvent!.ReasonCodeId);
        var notified = Assert.Single(f.Notifier.DowntimeReasonCalls);
        Assert.Equal(machineId, notified.MachineId);
        Assert.Equal(reasonCodeId, notified.ReasonCodeId);
    }

    [Fact]
    public async Task AttachReasonAsync_InactiveReasonCode_ThrowsValidationError()
    {
        var f = CreateUseCase();
        var (machineId, siteId, _) = await SeedMachineWithOpenDowntimeAsync(f);
        var reasonCodeId = f.ReasonCodes.Seed(siteId, "Jam", LossCategory.AvailabilityLoss);
        var reasonCode = await f.ReasonCodes.GetAsync(reasonCodeId);
        reasonCode!.Deactivate();
        await f.ReasonCodes.UpdateAsync(reasonCode);

        await Assert.ThrowsAsync<MasterDataValidationException>(() =>
            f.UseCase.AttachReasonAsync(CallerScope.Global, "Operator", machineId, reasonCodeId));
    }

    [Fact]
    public async Task AttachReasonAsync_ReasonCodeFromDifferentSite_ThrowsValidationError()
    {
        var f = CreateUseCase();
        var (machineId, _, _) = await SeedMachineWithOpenDowntimeAsync(f);
        var otherSiteId = Guid.NewGuid();
        var reasonCodeId = f.ReasonCodes.Seed(otherSiteId, "Jam", LossCategory.AvailabilityLoss);

        await Assert.ThrowsAsync<MasterDataValidationException>(() =>
            f.UseCase.AttachReasonAsync(CallerScope.Global, "Operator", machineId, reasonCodeId));
    }

    [Fact]
    public async Task AttachReasonAsync_NoOpenDowntimeEvent_ThrowsNotOpen()
    {
        var f = CreateUseCase();
        var siteId = Guid.NewGuid();
        var lineId = f.Lines.Seed("Line A", siteId);
        var machineId = f.Machines.Seed("Machine A", lineId, siteId);
        var reasonCodeId = f.ReasonCodes.Seed(siteId, "Jam", LossCategory.AvailabilityLoss);

        await Assert.ThrowsAsync<DowntimeEventNotOpenException>(() =>
            f.UseCase.AttachReasonAsync(CallerScope.Global, "Operator", machineId, reasonCodeId));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Viewer")]
    public async Task AttachReasonAsync_DisallowedRole_ThrowsForbidden(string role)
    {
        var f = CreateUseCase();
        var (machineId, siteId, _) = await SeedMachineWithOpenDowntimeAsync(f);
        var reasonCodeId = f.ReasonCodes.Seed(siteId, "Jam", LossCategory.AvailabilityLoss);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            f.UseCase.AttachReasonAsync(CallerScope.Global, role, machineId, reasonCodeId));
    }

    [Fact]
    public async Task AttachReasonAsync_OperatorScopedToTheMachinesOwnLine_Succeeds()
    {
        // Regression: an earlier version of this check compared the scope's allowed LineIds against
        // the *machine's own id* instead of its parent Line's id, which meant a correctly-scoped
        // Operator was always rejected — masked because every existing test used either the global
        // scope or a scope guaranteed not to match. This proves the correct-scope path actually works.
        var f = CreateUseCase();
        var (machineId, siteId, lineId) = await SeedMachineWithOpenDowntimeAsync(f);
        var reasonCodeId = f.ReasonCodes.Seed(siteId, "Jam", LossCategory.AvailabilityLoss);
        var scope = new CallerScope(false, [siteId], [lineId]);

        await f.UseCase.AttachReasonAsync(scope, "Operator", machineId, reasonCodeId);

        var openEvent = await f.DowntimeEvents.GetOpenByMachineIdAsync(machineId);
        Assert.Equal(reasonCodeId, openEvent!.ReasonCodeId);
    }

    [Fact]
    public async Task AttachReasonAsync_OperatorScopedToDifferentLine_ThrowsForbidden()
    {
        var f = CreateUseCase();
        var (machineId, siteId, _) = await SeedMachineWithOpenDowntimeAsync(f);
        var reasonCodeId = f.ReasonCodes.Seed(siteId, "Jam", LossCategory.AvailabilityLoss);
        var scope = new CallerScope(false, [siteId], [Guid.NewGuid()]);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() =>
            f.UseCase.AttachReasonAsync(scope, "Operator", machineId, reasonCodeId));
    }
}
