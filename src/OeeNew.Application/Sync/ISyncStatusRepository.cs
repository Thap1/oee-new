namespace OeeNew.Application.Sync;

/// <summary>Read-side projection of a <c>SiteSyncStatus</c> row (Story 5.3) — a plain data record, not the EF-mapped Infrastructure entity.</summary>
public sealed record SiteSyncStatusRecord(Guid SiteId, DateTimeOffset LastSyncedAt);

/// <summary>
/// Central-side write path for the sync status badge (Story 5.1) plus its read side (Story 5.3). Written
/// once per <see cref="SyncSiteRecord"/> actually present in a successfully ingested batch, not once per
/// "calling instance" — the demo/deploy seed can put more than one Site in a single database.
/// </summary>
public interface ISyncStatusRepository
{
    Task RecordSyncedAsync(Guid siteId, DateTimeOffset syncedAt, CancellationToken cancellationToken = default);

    /// <summary>Every recorded sync status, unfiltered — scope filtering happens one layer up in <see cref="SyncStatusQueryUseCase"/>, same pattern as <c>SiteManagementUseCase.ListAsync</c>.</summary>
    Task<IReadOnlyList<SiteSyncStatusRecord>> ListAllAsync(CancellationToken cancellationToken = default);
}
