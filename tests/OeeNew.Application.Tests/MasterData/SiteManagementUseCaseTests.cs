using OeeNew.Application;
using OeeNew.Application.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.MasterData;

public class SiteManagementUseCaseTests
{
    [Fact]
    public async Task CreateAsync_WithValidName_PersistsSiteWithGeneratedId()
    {
        var useCase = new SiteManagementUseCase(new FakeSiteRepository(), new FakeLineRepository(), new AppModeInfo("Site"));

        var site = await useCase.CreateAsync("Admin", "Site A");

        Assert.NotEqual(Guid.Empty, site.Id);
        Assert.Equal("Site A", site.Name);
    }

    [Fact]
    public async Task CreateAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = new SiteManagementUseCase(new FakeSiteRepository(), new FakeLineRepository(), new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.CreateAsync("Viewer", "Site A"));
    }

    [Fact]
    public async Task RenameAsync_WithExistingSite_UpdatesName()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Site"));

        var renamed = await useCase.RenameAsync("Admin", siteId, "Site B");

        Assert.Equal("Site B", renamed.Name);
    }

    [Fact]
    public async Task RenameAsync_WithUnknownSite_ThrowsNotFound()
    {
        var useCase = new SiteManagementUseCase(new FakeSiteRepository(), new FakeLineRepository(), new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => useCase.RenameAsync("Admin", Guid.NewGuid(), "Site B"));
    }

    [Fact]
    public async Task RenameAsync_AsNonAdmin_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.RenameAsync("Viewer", siteId, "Site B"));
    }

    [Fact]
    public async Task DeleteAsync_WithNoChildLines_RemovesSite()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Site"));

        await useCase.DeleteAsync("Admin", siteId);

        Assert.Null(await siteRepo.GetAsync(siteId));
    }

    [Fact]
    public async Task DeleteAsync_WithChildLines_ThrowsHasDependents()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        lineRepo.Seed("Line A", siteId);
        var useCase = new SiteManagementUseCase(siteRepo, lineRepo, new AppModeInfo("Site"));

        var ex = await Assert.ThrowsAsync<MasterDataHasDependentsException>(() => useCase.DeleteAsync("Admin", siteId));
        Assert.Contains("Line A", ex.DependentNames);
        Assert.NotNull(await siteRepo.GetAsync(siteId));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownSite_ThrowsNotFound()
    {
        var useCase = new SiteManagementUseCase(new FakeSiteRepository(), new FakeLineRepository(), new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => useCase.DeleteAsync("Admin", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_AsNonAdmin_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.DeleteAsync("Viewer", siteId));
    }

    [Fact]
    public async Task CreateAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var useCase = new SiteManagementUseCase(new FakeSiteRepository(), new FakeLineRepository(), new AppModeInfo("Central"));

        await Assert.ThrowsAsync<CentralReadOnlyException>(() => useCase.CreateAsync("Admin", "Site A"));
    }

    [Fact]
    public async Task RenameAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Central"));

        await Assert.ThrowsAsync<CentralReadOnlyException>(() => useCase.RenameAsync("Admin", siteId, "Site B"));
    }

    [Fact]
    public async Task DeleteAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var siteRepo = new FakeSiteRepository();
        var siteId = siteRepo.Seed("Site A");
        var useCase = new SiteManagementUseCase(siteRepo, new FakeLineRepository(), new AppModeInfo("Central"));

        await Assert.ThrowsAsync<CentralReadOnlyException>(() => useCase.DeleteAsync("Admin", siteId));
    }
}
