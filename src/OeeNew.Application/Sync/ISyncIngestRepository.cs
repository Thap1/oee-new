namespace OeeNew.Application.Sync;

/// <summary>Central-side idempotent write of an incoming <see cref="SyncBatch"/> (Story 5.1, AC #3/#4) — one call, one DB transaction.</summary>
public interface ISyncIngestRepository
{
    Task IngestAsync(SyncBatch batch, CancellationToken cancellationToken = default);
}
