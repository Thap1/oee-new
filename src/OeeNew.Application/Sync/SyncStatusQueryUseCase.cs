using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;

namespace OeeNew.Application.Sync;

public sealed record SiteSyncStatusResult(Guid SiteId, string SiteName, DateTimeOffset? LastSyncedAt, bool IsStale);

/// <summary>
/// Central-side sync status badge query (Story 5.3). Enumerates from <see cref="Domain.MasterData.Site"/>
/// first, left-joining <see cref="SiteSyncStatusRecord"/> — a Site the caller can see but that has never
/// completed a sync has no status row at all, and must still appear as "never synced" (always stale, AC
/// #1/#2), not be silently omitted the way iterating only existing status rows would.
/// </summary>
public sealed class SyncStatusQueryUseCase(ISiteRepository sites, ISyncStatusRepository syncStatuses)
{
    /// <summary>
    /// <paramref name="warningThresholdMinutes"/> is passed in, not read from <c>IOptions</c> here —
    /// Application must never depend on Infrastructure's <c>SyncOptions</c> (AD-1); the caller
    /// (<c>SyncStatusController</c>) resolves it, same as <c>ProductionStatusController</c> already does
    /// for <c>ProductionOptions.NoSignalThresholdSeconds</c> rather than threading <c>IOptions</c> into
    /// <c>MachineStatusQueryUseCase</c>.
    /// </summary>
    public async Task<IReadOnlyList<SiteSyncStatusResult>> GetStatusesAsync(
        CallerScope scope, int warningThresholdMinutes, CancellationToken cancellationToken = default)
    {
        var allSites = await sites.ListAsync(cancellationToken);
        var scopedSites = scope.IsGlobal ? allSites : allSites.Where(s => scope.AllowsSite(s.Id)).ToList();

        var statusesBySite = (await syncStatuses.ListAllAsync(cancellationToken)).ToDictionary(s => s.SiteId, s => s.LastSyncedAt);
        var threshold = TimeSpan.FromMinutes(warningThresholdMinutes);
        var now = DateTimeOffset.UtcNow;

        return scopedSites.Select(site =>
        {
            // Dictionary<Guid, DateTimeOffset>.GetValueOrDefault returns DateTimeOffset.MinValue (not
            // null) for a missing key, since DateTimeOffset is a non-nullable struct — TryGetValue is
            // the only way to distinguish "never synced" from a (impossible in practice) MinValue timestamp.
            var lastSyncedAt = statusesBySite.TryGetValue(site.Id, out var value) ? value : (DateTimeOffset?)null;
            // Never synced (null) always counts as stale (AC #2) — at least as noteworthy as merely behind schedule.
            var isStale = lastSyncedAt is null || now - lastSyncedAt.Value > threshold;
            return new SiteSyncStatusResult(site.Id, site.Name, lastSyncedAt, isStale);
        }).ToList();
    }
}
