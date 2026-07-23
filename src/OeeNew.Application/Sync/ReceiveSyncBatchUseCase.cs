namespace OeeNew.Application.Sync;

/// <summary>Central-side receive path (Story 5.1, AC #3/#4): ingest the batch, then record every Site in it as freshly synced.</summary>
public sealed class ReceiveSyncBatchUseCase(ISyncIngestRepository ingestRepository, ISyncStatusRepository statusRepository)
{
    public async Task IngestAsync(SyncBatch batch, CancellationToken cancellationToken = default)
    {
        await ingestRepository.IngestAsync(batch, cancellationToken);

        var syncedAt = DateTimeOffset.UtcNow;
        foreach (var site in batch.Sites)
        {
            await statusRepository.RecordSyncedAsync(site.Id, syncedAt, cancellationToken);
        }
    }
}
