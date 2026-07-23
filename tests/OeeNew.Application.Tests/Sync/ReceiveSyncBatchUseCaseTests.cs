using OeeNew.Application.Sync;
using Xunit;

namespace OeeNew.Application.Tests.Sync;

internal sealed class FakeSyncIngestRepository : ISyncIngestRepository
{
    public List<SyncBatch> IngestedBatches { get; } = [];

    public Task IngestAsync(SyncBatch batch, CancellationToken cancellationToken = default)
    {
        IngestedBatches.Add(batch);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSyncStatusRepository : ISyncStatusRepository
{
    public List<(Guid SiteId, DateTimeOffset SyncedAt)> Recorded { get; } = [];

    public Task RecordSyncedAsync(Guid siteId, DateTimeOffset syncedAt, CancellationToken cancellationToken = default)
    {
        Recorded.Add((siteId, syncedAt));
        return Task.CompletedTask;
    }
}

public class ReceiveSyncBatchUseCaseTests
{
    [Fact]
    public async Task IngestAsync_IngestsBatchThenRecordsSyncedForEverySiteInBatch_NotJustOne()
    {
        var ingestRepository = new FakeSyncIngestRepository();
        var statusRepository = new FakeSyncStatusRepository();
        var useCase = new ReceiveSyncBatchUseCase(ingestRepository, statusRepository);

        var siteAId = Guid.NewGuid();
        var siteBId = Guid.NewGuid();
        var batch = new SyncBatch(
            [new SyncSiteRecord(siteAId, "Site A"), new SyncSiteRecord(siteBId, "Site B")],
            [], [], [], [], []);

        await useCase.IngestAsync(batch);

        Assert.Single(ingestRepository.IngestedBatches);
        Assert.Equal(2, statusRepository.Recorded.Count);
        Assert.Contains(statusRepository.Recorded, r => r.SiteId == siteAId);
        Assert.Contains(statusRepository.Recorded, r => r.SiteId == siteBId);
    }

    [Fact]
    public async Task IngestAsync_EmptySiteList_RecordsNoSyncStatus()
    {
        var ingestRepository = new FakeSyncIngestRepository();
        var statusRepository = new FakeSyncStatusRepository();
        var useCase = new ReceiveSyncBatchUseCase(ingestRepository, statusRepository);

        var batch = new SyncBatch([], [], [], [], [], []);

        await useCase.IngestAsync(batch);

        Assert.Single(ingestRepository.IngestedBatches);
        Assert.Empty(statusRepository.Recorded);
    }
}
