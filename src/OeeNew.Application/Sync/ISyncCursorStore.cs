namespace OeeNew.Application.Sync;

/// <summary>
/// Site-side bookkeeping for the sync push loop (Story 5.1) — tracks the instant up to which
/// closed <c>DowntimeEvent</c>/<c>QualityReject</c> records have already been pushed to Central, so
/// each cycle only gathers what's new. Always unset on a Central instance (nothing to push from there).
/// </summary>
public interface ISyncCursorStore
{
    Task<DateTimeOffset?> GetLastPushedAtAsync(CancellationToken cancellationToken = default);
    Task SetLastPushedAtAsync(DateTimeOffset value, CancellationToken cancellationToken = default);
}
