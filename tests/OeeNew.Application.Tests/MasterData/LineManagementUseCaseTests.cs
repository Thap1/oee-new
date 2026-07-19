using OeeNew.Application.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.MasterData;

public class LineManagementUseCaseTests
{
    [Fact]
    public async Task CreateAsync_WithExistingSite_PersistsLine()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new LineManagementUseCase(new FakeLineRepository(), siteRepo, new FakeMachineRepository());

        var line = await useCase.CreateAsync("Admin", siteId, "Line A");

        Assert.NotEqual(Guid.Empty, line.Id);
        Assert.Equal(siteId, line.SiteId);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownSite_ThrowsParentNotFound()
    {
        var useCase = new LineManagementUseCase(new FakeLineRepository(), new FakeSiteRepository(), new FakeMachineRepository());

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(() => useCase.CreateAsync("Admin", Guid.NewGuid(), "Line A"));
    }

    [Fact]
    public async Task CreateAsync_AsNonAdmin_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new LineManagementUseCase(new FakeLineRepository(), siteRepo, new FakeMachineRepository());

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.CreateAsync("Viewer", siteId, "Line A"));
    }

    [Fact]
    public async Task RenameAsync_WithUnknownLine_ThrowsNotFound()
    {
        var useCase = new LineManagementUseCase(new FakeLineRepository(), new FakeSiteRepository(), new FakeMachineRepository());

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => useCase.RenameAsync("Admin", Guid.NewGuid(), "Line B"));
    }

    [Fact]
    public async Task DeleteAsync_WithChildMachines_ThrowsHasDependents()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var machineRepo = new FakeMachineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        machineRepo.Seed("Machine A", lineId);
        var useCase = new LineManagementUseCase(lineRepo, siteRepo, machineRepo);

        var ex = await Assert.ThrowsAsync<MasterDataHasDependentsException>(() => useCase.DeleteAsync("Admin", lineId));
        Assert.Contains("Machine A", ex.DependentNames);
        Assert.NotNull(await lineRepo.GetAsync(lineId));
    }

    [Fact]
    public async Task DeleteAsync_WithNoChildMachines_RemovesLine()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var useCase = new LineManagementUseCase(lineRepo, siteRepo, new FakeMachineRepository());

        await useCase.DeleteAsync("Admin", lineId);

        Assert.Null(await lineRepo.GetAsync(lineId));
    }

    [Fact]
    public async Task DeleteAsync_AsNonAdmin_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var useCase = new LineManagementUseCase(lineRepo, siteRepo, new FakeMachineRepository());

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.DeleteAsync("Viewer", lineId));
    }
}
