using OeeNew.Application.Auth;
using OeeNew.Application.Sync;
using OeeNew.Application.Tests.MasterData;
using Xunit;

namespace OeeNew.Application.Tests.Sync;

public class SyncStatusQueryUseCaseTests
{
    private const int WarningThresholdMinutes = 15;

    [Fact]
    public async Task GetStatusesAsync_RecentSync_IsNotStale()
    {
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var statuses = new FakeSyncStatusRepository();
        await statuses.RecordSyncedAsync(siteId, DateTimeOffset.UtcNow.AddMinutes(-1));
        var useCase = new SyncStatusQueryUseCase(sites, statuses);

        var result = await useCase.GetStatusesAsync(CallerScope.Global, WarningThresholdMinutes);

        var status = Assert.Single(result);
        Assert.Equal(siteId, status.SiteId);
        Assert.NotNull(status.LastSyncedAt);
        Assert.False(status.IsStale);
    }

    [Fact]
    public async Task GetStatusesAsync_SyncOlderThanThreshold_IsStale()
    {
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var statuses = new FakeSyncStatusRepository();
        await statuses.RecordSyncedAsync(siteId, DateTimeOffset.UtcNow.AddMinutes(-(WarningThresholdMinutes + 1)));
        var useCase = new SyncStatusQueryUseCase(sites, statuses);

        var result = await useCase.GetStatusesAsync(CallerScope.Global, WarningThresholdMinutes);

        var status = Assert.Single(result);
        Assert.True(status.IsStale);
    }

    [Fact]
    public async Task GetStatusesAsync_SiteWithNoSyncStatusRowAtAll_StillAppearsAsNeverSyncedAndStale()
    {
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var statuses = new FakeSyncStatusRepository();
        var useCase = new SyncStatusQueryUseCase(sites, statuses);

        var result = await useCase.GetStatusesAsync(CallerScope.Global, WarningThresholdMinutes);

        var status = Assert.Single(result);
        Assert.Equal(siteId, status.SiteId);
        Assert.Null(status.LastSyncedAt);
        Assert.True(status.IsStale);
    }

    [Fact]
    public async Task GetStatusesAsync_ManagerScopedToSiteA_NeverSeesSiteBsStatus()
    {
        var sites = new FakeSiteRepository();
        var siteAId = sites.Seed("Site A");
        var siteBId = sites.Seed("Site B");
        var statuses = new FakeSyncStatusRepository();
        await statuses.RecordSyncedAsync(siteBId, DateTimeOffset.UtcNow.AddDays(-30));
        var useCase = new SyncStatusQueryUseCase(sites, statuses);
        var scope = new CallerScope(false, [siteAId], []);

        var result = await useCase.GetStatusesAsync(scope, WarningThresholdMinutes);

        var status = Assert.Single(result);
        Assert.Equal(siteAId, status.SiteId);
    }
}
