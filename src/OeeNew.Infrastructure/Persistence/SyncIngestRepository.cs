using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Sync;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Persistence;

/// <summary>
/// Central-side idempotent ingest of a Site's <see cref="SyncBatch"/> (Story 5.1, AC #3/#4). Applies in
/// strict FK-dependency order (Site → Line → Machine → ReasonCode → DowntimeEvent/QualityReject) inside
/// one transaction: master data is a find-or-add upsert (small tables, resent as a full snapshot every
/// cycle); DowntimeEvent/QualityReject are insert-only, skipped if the Id already exists, since those are
/// immutable closed business records once synced and a retried push (AC #2) must be a no-op, not a
/// duplicate or an error.
/// </summary>
public sealed class SyncIngestRepository(OeeDbContext context) : ISyncIngestRepository
{
    public async Task IngestAsync(SyncBatch batch, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var record in batch.Sites)
        {
            var existing = await context.Sites.FindAsync([record.Id], cancellationToken);
            if (existing is null)
            {
                context.Sites.Add(new Site(record.Id, record.Name));
            }
            else
            {
                existing.Rename(record.Name);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var record in batch.Lines)
        {
            var existing = await context.Lines.FindAsync([record.Id], cancellationToken);
            if (existing is null)
            {
                context.Lines.Add(new Line(record.Id, record.Name, record.SiteId));
            }
            else
            {
                existing.Rename(record.Name);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var record in batch.Machines)
        {
            var existing = await context.Machines.FindAsync([record.Id], cancellationToken);
            if (existing is null)
            {
                context.Machines.Add(new Machine(record.Id, record.Name, record.LineId));
            }
            else
            {
                existing.Rename(record.Name);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var record in batch.ReasonCodes)
        {
            var existing = await context.ReasonCodes.FindAsync([record.Id], cancellationToken);
            if (existing is null)
            {
                context.ReasonCodes.Add(new ReasonCode(record.Id, record.SiteId, record.Name, record.LossCategory));
            }
            else
            {
                // Known, accepted gap (Story 5.1 Dev Notes): ReasonCode's Domain type exposes only
                // Deactivate() — no Rename()/Activate()/LossCategory mutator — so an existing row's
                // Name/LossCategory can't be updated in place here, and reactivation doesn't sync either.
                if (!record.IsActive && existing.IsActive)
                {
                    existing.Deactivate();
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var record in batch.DowntimeEvents)
        {
            var alreadyExists = await context.DowntimeEvents.AnyAsync(d => d.Id == record.Id, cancellationToken);
            if (alreadyExists)
            {
                continue;
            }

            var downtimeEvent = new DowntimeEvent(record.Id, record.MachineId, record.StartedAt);
            if (record.ReasonCodeId is { } reasonCodeId)
            {
                downtimeEvent.AssignReason(reasonCodeId);
            }

            downtimeEvent.Close(record.EndedAt);
            context.DowntimeEvents.Add(downtimeEvent);
        }

        foreach (var record in batch.QualityRejects)
        {
            var alreadyExists = await context.QualityRejects.AnyAsync(q => q.Id == record.Id, cancellationToken);
            if (alreadyExists)
            {
                continue;
            }

            context.QualityRejects.Add(new QualityReject(record.Id, record.MachineId, record.Quantity, record.RecordedAt));
        }

        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
