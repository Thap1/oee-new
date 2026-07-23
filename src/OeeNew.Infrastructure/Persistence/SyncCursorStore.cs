using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Sync;

namespace OeeNew.Infrastructure.Persistence;

public sealed class SyncCursorStore(OeeDbContext context) : ISyncCursorStore
{
    private const short RowId = 1;

    public async Task<DateTimeOffset?> GetLastPushedAtAsync(CancellationToken cancellationToken = default)
    {
        var row = await context.SyncCursor.FirstOrDefaultAsync(r => r.Id == RowId, cancellationToken);
        return row?.LastPushedAt;
    }

    public async Task SetLastPushedAtAsync(DateTimeOffset value, CancellationToken cancellationToken = default)
    {
        var row = await context.SyncCursor.FirstOrDefaultAsync(r => r.Id == RowId, cancellationToken);
        if (row is null)
        {
            context.SyncCursor.Add(new SyncCursorRow { Id = RowId, LastPushedAt = value });
        }
        else
        {
            row.LastPushedAt = value;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
