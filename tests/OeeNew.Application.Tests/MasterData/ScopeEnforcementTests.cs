using OeeNew.Application;
using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Tests.Production;
using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.MasterData;

/// <summary>Story 1.6, AC #2/#4 — List/read use cases must filter by the caller's CallerScope, not just gate writes behind AdminOnly.</summary>
public class ScopeEnforcementTests
{
    [Fact]
    public async Task Sites_ListAsync_Global_ReturnsAllSites()
    {
        var siteRepo = new FakeSiteRepository();
        var siteA = siteRepo.Seed("Site A");
        var siteB = siteRepo.Seed("Site B");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Site"));

        var result = await useCase.ListAsync(CallerScope.Global);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Id == siteA);
        Assert.Contains(result, s => s.Id == siteB);
    }

    [Fact]
    public async Task Sites_ListAsync_Scoped_ReturnsOnlyAssignedSite()
    {
        var siteRepo = new FakeSiteRepository();
        var siteA = siteRepo.Seed("Site A");
        siteRepo.Seed("Site B");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [siteA], []);

        var result = await useCase.ListAsync(scope);

        Assert.Single(result);
        Assert.Equal(siteA, result[0].Id);
    }

    [Fact]
    public async Task Lines_ListBySiteAsync_InScopeSite_ReturnsLines()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        lineRepo.Seed("Line A", siteId);
        var useCase = new LineManagementUseCase(lineRepo, siteRepo, new FakeMachineRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [siteId], []);

        var result = await useCase.ListBySiteAsync(scope, siteId);

        Assert.Single(result);
    }

    [Fact]
    public async Task Lines_ListBySiteAsync_OutOfScopeSite_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new LineManagementUseCase(lineRepo, siteRepo, new FakeMachineRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [Guid.NewGuid()], []);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.ListBySiteAsync(scope, siteId));
    }

    [Fact]
    public async Task Lines_ListBySiteAsync_OperatorRestrictedToOneLine_HidesOtherLinesInSameSite()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineA = lineRepo.Seed("Line A", siteId);
        lineRepo.Seed("Line B", siteId);
        var useCase = new LineManagementUseCase(lineRepo, siteRepo, new FakeMachineRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [siteId], [lineA]);

        var result = await useCase.ListBySiteAsync(scope, siteId);

        Assert.Single(result);
        Assert.Equal(lineA, result[0].Id);
    }

    [Fact]
    public async Task Machines_ListByLineAsync_OutOfScopeLine_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var useCase = new MachineManagementUseCase(new FakeMachineRepository(), lineRepo, new AppModeInfo("Site"));
        var scope = new CallerScope(false, [Guid.NewGuid()], []);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.ListByLineAsync(scope, lineId));
    }

    [Fact]
    public async Task Machines_ListByLineAsync_NonexistentLine_ReturnsEmptyWithoutThrowing()
    {
        var useCase = new MachineManagementUseCase(new FakeMachineRepository(), new FakeLineRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [Guid.NewGuid()], []);

        var result = await useCase.ListByLineAsync(scope, Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task ShiftSchedules_ListBySiteAsync_OutOfScopeSite_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new ShiftScheduleManagementUseCase(new FakeShiftScheduleRepository(), siteRepo, new FakeLineRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [Guid.NewGuid()], []);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.ListBySiteAsync(scope, siteId));
    }

    [Fact]
    public async Task ReasonCodes_ListBySiteAsync_OutOfScopeSite_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new ReasonCodeManagementUseCase(new FakeReasonCodeRepository(), siteRepo, new FakeDowntimeEventRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [Guid.NewGuid()], []);

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.ListBySiteAsync(scope, siteId));
    }

    [Fact]
    public async Task ReasonCodes_ListBySiteAsync_InScopeSite_ReturnsReasonCodes()
    {
        var siteRepo = new FakeSiteRepository();
        var reasonCodeRepo = new FakeReasonCodeRepository();
        var siteId = siteRepo.Seed("Site A");
        reasonCodeRepo.Seed(siteId, "Changeover", LossCategory.PerformanceLoss);
        var useCase = new ReasonCodeManagementUseCase(reasonCodeRepo, siteRepo, new FakeDowntimeEventRepository(), new AppModeInfo("Site"));
        var scope = new CallerScope(false, [siteId], []);

        var result = await useCase.ListBySiteAsync(scope, siteId);

        Assert.Single(result);
    }
}
