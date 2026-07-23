using OeeNew.Application;
using OeeNew.Application.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.MasterData;

public class MachineManagementUseCaseTests
{
    [Fact]
    public async Task CreateAsync_WithExistingLine_PersistsMachine()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var useCase = new MachineManagementUseCase(new FakeMachineRepository(), lineRepo, new AppModeInfo("Site"));

        var machine = await useCase.CreateAsync("Admin", lineId, "Machine A");

        Assert.NotEqual(Guid.Empty, machine.Id);
        Assert.Equal(lineId, machine.LineId);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownLine_ThrowsParentNotFound()
    {
        var useCase = new MachineManagementUseCase(new FakeMachineRepository(), new FakeLineRepository(), new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(() => useCase.CreateAsync("Admin", Guid.NewGuid(), "Machine A"));
    }

    [Fact]
    public async Task CreateAsync_AsNonAdmin_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var useCase = new MachineManagementUseCase(new FakeMachineRepository(), lineRepo, new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.CreateAsync("Viewer", lineId, "Machine A"));
    }

    [Fact]
    public async Task DeleteAsync_WithExistingMachine_RemovesIt()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var machineRepo = new FakeMachineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var machineId = machineRepo.Seed("Machine A", lineId);
        var useCase = new MachineManagementUseCase(machineRepo, lineRepo, new AppModeInfo("Site"));

        await useCase.DeleteAsync("Admin", machineId);

        Assert.Null(await machineRepo.GetAsync(machineId));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownMachine_ThrowsNotFound()
    {
        var useCase = new MachineManagementUseCase(new FakeMachineRepository(), new FakeLineRepository(), new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => useCase.DeleteAsync("Admin", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_AsNonAdmin_ThrowsForbidden()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var machineRepo = new FakeMachineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var machineId = machineRepo.Seed("Machine A", lineId);
        var useCase = new MachineManagementUseCase(machineRepo, lineRepo, new AppModeInfo("Site"));

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.DeleteAsync("Viewer", machineId));
    }

    [Fact]
    public async Task CreateAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var useCase = new MachineManagementUseCase(new FakeMachineRepository(), lineRepo, new AppModeInfo("Central"));

        await Assert.ThrowsAsync<CentralReadOnlyException>(() => useCase.CreateAsync("Admin", lineId, "Machine A"));
    }

    [Fact]
    public async Task DeleteAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var siteRepo = new FakeSiteRepository();
        var lineRepo = new FakeLineRepository();
        var machineRepo = new FakeMachineRepository();
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        var machineId = machineRepo.Seed("Machine A", lineId);
        var useCase = new MachineManagementUseCase(machineRepo, lineRepo, new AppModeInfo("Central"));

        await Assert.ThrowsAsync<CentralReadOnlyException>(() => useCase.DeleteAsync("Admin", machineId));
    }
}
