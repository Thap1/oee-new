namespace OeeNew.Application.Sync;

/// <summary>
/// Central-side write path for the sync status badge (Story 5.3 owns the read side). Called once per
/// <see cref="SyncSiteRecord"/> actually present in a successfully ingested batch, not once per "calling
/// instance" — the demo/deploy seed can put more than one Site in a single database.
/// </summary>
public interface ISyncStatusRepository
{
    Task RecordSyncedAsync(Guid siteId, DateTimeOffset syncedAt, CancellationToken cancellationToken = default);
}
