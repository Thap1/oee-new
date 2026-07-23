namespace OeeNew.Application.Sync;

/// <summary>Site-side transport abstraction for pushing a <see cref="SyncBatch"/> to Central (Story 5.1, AD-1: Application never calls HttpClient directly).</summary>
public interface ISyncClient
{
    /// <summary>Returns false (not thrown) on any transport/HTTP failure — <see cref="PushSyncBatchUseCase"/> treats that as "try again next cycle," not an error to propagate (AC #2).</summary>
    Task<bool> TryPushAsync(SyncBatch batch, CancellationToken cancellationToken = default);
}
