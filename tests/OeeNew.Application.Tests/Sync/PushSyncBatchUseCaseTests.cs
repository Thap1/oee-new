using OeeNew.Application.Sync;
using OeeNew.Application.Tests.MasterData;
using OeeNew.Application.Tests.Production;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Application.Tests.Sync;

internal sealed class FakeSyncClient : ISyncClient
{
    public List<SyncBatch> PushedBatches { get; } = [];
    public bool ShouldSucceed { get; set; } = true;

    public Task<bool> TryPushAsync(SyncBatch batch, CancellationToken cancellationToken = default)
    {
        PushedBatches.Add(batch);
        return Task.FromResult(ShouldSucceed);
    }
}

internal sealed class FakeSyncCursorStore : ISyncCursorStore
{
    private DateTimeOffset? _lastPushedAt;

    public Task<DateTimeOffset?> GetLastPushedAtAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_lastPushedAt);

    public Task SetLastPushedAtAsync(DateTimeOffset value, CancellationToken cancellationToken = default)
    {
        _lastPushedAt = value;
        return Task.CompletedTask;
    }
}

public class PushSyncBatchUseCaseTests
{
    private static PushSyncBatchUseCase BuildUseCase(
        FakeSiteRepository sites, FakeLineRepository lines, FakeMachineRepository machines,
        FakeReasonCodeRepository reasonCodes, FakeDowntimeEventRepository downtimeEvents,
        FakeQualityRejectRepository qualityRejects, FakeSyncClient syncClient, FakeSyncCursorStore cursorStore) =>
        new(sites, lines, machines, reasonCodes, downtimeEvents, qualityRejects, syncClient, cursorStore);

    [Fact]
    public async Task RunOnceAsync_NoPriorCursor_PushesEverythingCurrentlyInRepositories()
    {
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var lines = new FakeLineRepository();
        var lineId = lines.Seed("Line A", siteId);
        var machines = new FakeMachineRepository();
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var reasonCodes = new FakeReasonCodeRepository();
        reasonCodes.Seed(siteId, "Jam", LossCategory.AvailabilityLoss);
        var downtimeEvents = new FakeDowntimeEventRepository();
        var closedEvent = new DowntimeEvent(Guid.NewGuid(), machineId, new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero));
        closedEvent.Close(new DateTimeOffset(2026, 7, 20, 8, 5, 0, TimeSpan.Zero));
        downtimeEvents.SeedEvent(closedEvent);
        var qualityRejects = new FakeQualityRejectRepository();
        await qualityRejects.AddAsync(new QualityReject(Guid.NewGuid(), machineId, 3, new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero)));

        var syncClient = new FakeSyncClient();
        var cursorStore = new FakeSyncCursorStore();
        var useCase = BuildUseCase(sites, lines, machines, reasonCodes, downtimeEvents, qualityRejects, syncClient, cursorStore);

        var pushed = await useCase.RunOnceAsync();

        Assert.True(pushed);
        var batch = Assert.Single(syncClient.PushedBatches);
        Assert.Single(batch.Sites);
        Assert.Single(batch.Lines);
        Assert.Single(batch.Machines);
        Assert.Single(batch.ReasonCodes);
        Assert.Single(batch.DowntimeEvents);
        Assert.Single(batch.QualityRejects);
    }

    [Fact]
    public async Task RunOnceAsync_SecondRunAfterSuccess_OnlyIncludesEventsClosedAfterFirstRunsCycleStart()
    {
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var lines = new FakeLineRepository();
        var lineId = lines.Seed("Line A", siteId);
        var machines = new FakeMachineRepository();
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var reasonCodes = new FakeReasonCodeRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();

        var firstEvent = new DowntimeEvent(Guid.NewGuid(), machineId, DateTimeOffset.UtcNow.AddMinutes(-10));
        firstEvent.Close(DateTimeOffset.UtcNow.AddMinutes(-9));
        downtimeEvents.SeedEvent(firstEvent);

        var syncClient = new FakeSyncClient();
        var cursorStore = new FakeSyncCursorStore();
        var useCase = BuildUseCase(sites, lines, machines, reasonCodes, downtimeEvents, qualityRejects, syncClient, cursorStore);

        var firstPushed = await useCase.RunOnceAsync();
        Assert.True(firstPushed);
        Assert.Single(syncClient.PushedBatches[0].DowntimeEvents);

        // Closed strictly after the first run's captured cycleStartedAt.
        var secondEvent = new DowntimeEvent(Guid.NewGuid(), machineId, DateTimeOffset.UtcNow.AddSeconds(1));
        secondEvent.Close(DateTimeOffset.UtcNow.AddSeconds(2));
        downtimeEvents.SeedEvent(secondEvent);

        var secondPushed = await useCase.RunOnceAsync();

        Assert.True(secondPushed);
        var secondBatchEvent = Assert.Single(syncClient.PushedBatches[1].DowntimeEvents);
        Assert.Equal(secondEvent.Id, secondBatchEvent.Id);
    }

    [Fact]
    public async Task RunOnceAsync_PushFails_LeavesCursorUnchanged()
    {
        var sites = new FakeSiteRepository();
        var siteId = sites.Seed("Site A");
        var lines = new FakeLineRepository();
        var lineId = lines.Seed("Line A", siteId);
        var machines = new FakeMachineRepository();
        var machineId = machines.Seed("Machine A", lineId, siteId);
        var reasonCodes = new FakeReasonCodeRepository();
        var downtimeEvents = new FakeDowntimeEventRepository();
        var qualityRejects = new FakeQualityRejectRepository();

        var firstEvent = new DowntimeEvent(Guid.NewGuid(), machineId, DateTimeOffset.UtcNow.AddMinutes(-10));
        firstEvent.Close(DateTimeOffset.UtcNow.AddMinutes(-9));
        downtimeEvents.SeedEvent(firstEvent);

        var syncClient = new FakeSyncClient { ShouldSucceed = false };
        var cursorStore = new FakeSyncCursorStore();
        var useCase = BuildUseCase(sites, lines, machines, reasonCodes, downtimeEvents, qualityRejects, syncClient, cursorStore);

        var pushed = await useCase.RunOnceAsync();
        Assert.False(pushed);
        Assert.Null(await cursorStore.GetLastPushedAtAsync());

        // A second run after the failed push should still include the same not-yet-sent event.
        syncClient.ShouldSucceed = true;
        var secondPushed = await useCase.RunOnceAsync();
        Assert.True(secondPushed);
        Assert.Single(syncClient.PushedBatches[1].DowntimeEvents);
    }
}
