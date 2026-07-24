using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Sync;

namespace OeeNew.Infrastructure.Persistence;

public sealed class SyncStatusRepository(OeeDbContext context) : ISyncStatusRepository
{
    public async Task RecordSyncedAsync(Guid siteId, DateTimeOffset syncedAt, CancellationToken cancellationToken = default)
    {
        var existing = await context.SiteSyncStatuses.FirstOrDefaultAsync(s => s.SiteId == siteId, cancellationToken);
        if (existing is null)
        {
            context.SiteSyncStatuses.Add(new SiteSyncStatus { SiteId = siteId, LastSyncedAt = syncedAt });
        }
        else
        {
            existing.LastSyncedAt = syncedAt;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SiteSyncStatusRecord>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await context.SiteSyncStatuses
            .Select(s => new SiteSyncStatusRecord(s.SiteId, s.LastSyncedAt))
            .ToListAsync(cancellationToken);
}
