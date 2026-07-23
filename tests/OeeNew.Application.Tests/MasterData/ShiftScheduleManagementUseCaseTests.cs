using OeeNew.Application;
using OeeNew.Application.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.MasterData;

public class ShiftScheduleManagementUseCaseTests
{
    private static ShiftScheduleManagementUseCase CreateUseCase(
        out FakeSiteRepository siteRepo, out FakeLineRepository lineRepo, out FakeShiftScheduleRepository shiftRepo,
        AppModeInfo? appMode = null)
    {
        siteRepo = new FakeSiteRepository();
        lineRepo = new FakeLineRepository();
        shiftRepo = new FakeShiftScheduleRepository();
        return new ShiftScheduleManagementUseCase(shiftRepo, siteRepo, lineRepo, appMode ?? new AppModeInfo("Site"));
    }

    [Fact]
    public async Task CreateAsync_WithExistingSite_PersistsShift()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out _);
        var siteId = siteRepo.Seed("Site A");

        var shift = await useCase.CreateAsync("Admin", siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.NotEqual(Guid.Empty, shift.Id);
        Assert.Equal(siteId, shift.SiteId);
        Assert.Null(shift.LineId);
    }

    [Fact]
    public async Task CreateAsync_WithExistingLineUnderSite_PersistsShift()
    {
        var useCase = CreateUseCase(out var siteRepo, out var lineRepo, out _);
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);

        var shift = await useCase.CreateAsync("Admin", siteId, lineId, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.Equal(lineId, shift.LineId);
    }

    [Fact]
    public async Task CreateAsync_WithUnknownSite_ThrowsParentNotFound()
    {
        var useCase = CreateUseCase(out _, out _, out _);

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(
            () => useCase.CreateAsync("Admin", Guid.NewGuid(), null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public async Task CreateAsync_WithLineFromDifferentSite_ThrowsParentNotFound()
    {
        var useCase = CreateUseCase(out var siteRepo, out var lineRepo, out _);
        var siteId = siteRepo.Seed("Site A");
        var otherSiteId = siteRepo.Seed("Site B");
        var lineUnderOtherSite = lineRepo.Seed("Line B", otherSiteId);

        await Assert.ThrowsAsync<MasterDataParentNotFoundException>(
            () => useCase.CreateAsync("Admin", siteId, lineUnderOtherSite, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public async Task CreateAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out _);
        var siteId = siteRepo.Seed("Site A");

        await Assert.ThrowsAsync<MasterDataForbiddenException>(
            () => useCase.CreateAsync("Viewer", siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingShiftSameScope_ThrowsShiftOverlap()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        shiftRepo.Seed(siteId, null, "Morning", new TimeOnly(8, 0), new TimeOnly(12, 0));

        var ex = await Assert.ThrowsAsync<ShiftOverlapException>(
            () => useCase.CreateAsync("Admin", siteId, null, "Overlap", new TimeOnly(10, 0), new TimeOnly(14, 0)));

        Assert.Equal("Morning", ex.ConflictingShiftName);
    }

    [Fact]
    public async Task CreateAsync_WithAdjacentNonOverlappingShift_Succeeds()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        shiftRepo.Seed(siteId, null, "Morning", new TimeOnly(8, 0), new TimeOnly(12, 0));

        var shift = await useCase.CreateAsync("Admin", siteId, null, "Afternoon", new TimeOnly(12, 0), new TimeOnly(16, 0));

        Assert.Equal("Afternoon", shift.Name);
    }

    [Fact]
    public async Task CreateAsync_WithOverlappingShiftDifferentLineScope_Succeeds()
    {
        var useCase = CreateUseCase(out var siteRepo, out var lineRepo, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        var lineId = lineRepo.Seed("Line A", siteId);
        shiftRepo.Seed(siteId, null, "Site-wide", new TimeOnly(8, 0), new TimeOnly(16, 0));

        var shift = await useCase.CreateAsync("Admin", siteId, lineId, "Line-scoped", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.Equal(lineId, shift.LineId);
    }

    [Fact]
    public async Task RescheduleAsync_WithUnknownShift_ThrowsNotFound()
    {
        var useCase = CreateUseCase(out _, out _, out _);

        await Assert.ThrowsAsync<MasterDataNotFoundException>(
            () => useCase.RescheduleAsync("Admin", Guid.NewGuid(), "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public async Task RescheduleAsync_ToNonOverlappingTimes_UpdatesShift()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        var shiftId = shiftRepo.Seed(siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        var updated = await useCase.RescheduleAsync("Admin", shiftId, "Day Shift", new TimeOnly(9, 0), new TimeOnly(17, 0));

        Assert.Equal(new TimeOnly(9, 0), updated.StartTime);
        Assert.Equal(new TimeOnly(17, 0), updated.EndTime);
    }

    [Fact]
    public async Task RescheduleAsync_OverlappingAnotherShift_ThrowsShiftOverlap()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        shiftRepo.Seed(siteId, null, "Morning", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var afternoonId = shiftRepo.Seed(siteId, null, "Afternoon", new TimeOnly(12, 0), new TimeOnly(16, 0));

        await Assert.ThrowsAsync<ShiftOverlapException>(
            () => useCase.RescheduleAsync("Admin", afternoonId, "Afternoon", new TimeOnly(11, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public async Task RescheduleAsync_KeepingOwnTimeWindow_DoesNotConflictWithItself()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        var shiftId = shiftRepo.Seed(siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        var updated = await useCase.RescheduleAsync("Admin", shiftId, "Renamed", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.Equal("Renamed", updated.Name);
    }

    [Fact]
    public async Task RescheduleAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        var shiftId = shiftRepo.Seed(siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        await Assert.ThrowsAsync<MasterDataForbiddenException>(
            () => useCase.RescheduleAsync("Viewer", shiftId, "Day Shift", new TimeOnly(9, 0), new TimeOnly(17, 0)));
    }

    [Fact]
    public async Task DeleteAsync_WithExistingShift_RemovesIt()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        var shiftId = shiftRepo.Seed(siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        await useCase.DeleteAsync("Admin", shiftId);

        Assert.Null(await shiftRepo.GetAsync(shiftId));
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownShift_ThrowsNotFound()
    {
        var useCase = CreateUseCase(out _, out _, out _);

        await Assert.ThrowsAsync<MasterDataNotFoundException>(() => useCase.DeleteAsync("Admin", Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_AsNonAdmin_ThrowsForbidden()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo);
        var siteId = siteRepo.Seed("Site A");
        var shiftId = shiftRepo.Seed(siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        await Assert.ThrowsAsync<MasterDataForbiddenException>(() => useCase.DeleteAsync("Viewer", shiftId));
    }

    [Fact]
    public async Task CreateAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out _, new AppModeInfo("Central"));
        var siteId = siteRepo.Seed("Site A");

        await Assert.ThrowsAsync<CentralReadOnlyException>(
            () => useCase.CreateAsync("Admin", siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public async Task RescheduleAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo, new AppModeInfo("Central"));
        var siteId = siteRepo.Seed("Site A");
        var shiftId = shiftRepo.Seed(siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        await Assert.ThrowsAsync<CentralReadOnlyException>(
            () => useCase.RescheduleAsync("Admin", shiftId, "Day Shift", new TimeOnly(9, 0), new TimeOnly(17, 0)));
    }

    [Fact]
    public async Task DeleteAsync_AtCentral_ThrowsCentralReadOnly()
    {
        var useCase = CreateUseCase(out var siteRepo, out _, out var shiftRepo, new AppModeInfo("Central"));
        var siteId = siteRepo.Seed("Site A");
        var shiftId = shiftRepo.Seed(siteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        await Assert.ThrowsAsync<CentralReadOnlyException>(() => useCase.DeleteAsync("Admin", shiftId));
    }
}
