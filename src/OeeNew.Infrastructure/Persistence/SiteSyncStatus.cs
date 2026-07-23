namespace OeeNew.Infrastructure.Persistence;

/// <summary>
/// Central-side record of when each Site last successfully landed a sync batch (Story 5.1 write path;
/// Story 5.3 reads it for the sync status badge). Keyed by <see cref="SiteId"/>, not "calling instance" —
/// recorded once per <c>SyncSiteRecord</c> actually present in an ingested batch (see
/// <c>ReceiveSyncBatchUseCase</c>), since the demo/deploy seed can put more than one Site in a
/// single database. Always empty on a Site instance.
/// </summary>
public sealed class SiteSyncStatus
{
    public Guid SiteId { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }
}
