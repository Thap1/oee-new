using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;

namespace OeeNew.Application.Sync;

/// <summary>
/// Site-side "gather what's new, push it, advance the cursor" cycle (Story 5.1, AC #1/#2/#3) — driven
/// on a timer by <c>SyncPushHostedService</c>. Master data (Site/Line/Machine/ReasonCode) is resent as a
/// full snapshot every cycle since those tables are small and untracked for changes; only closed
/// <see cref="Production.DowntimeEvent"/>/<see cref="Production.QualityReject"/> use the cursor, since
/// those can grow unbounded.
/// </summary>
public sealed class PushSyncBatchUseCase(
    ISiteRepository sites,
    ILineRepository lines,
    IMachineRepository machines,
    IReasonCodeRepository reasonCodes,
    IDowntimeEventRepository downtimeEvents,
    IQualityRejectRepository qualityRejects,
    ISyncClient syncClient,
    ISyncCursorStore cursorStore)
{
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var cycleStartedAt = DateTimeOffset.UtcNow;
        var since = await cursorStore.GetLastPushedAtAsync(cancellationToken) ?? DateTimeOffset.MinValue;

        var allSites = await sites.ListAsync(cancellationToken);

        var siteRecords = allSites.Select(s => new SyncSiteRecord(s.Id, s.Name)).ToList();

        var lineRecords = new List<SyncLineRecord>();
        var reasonCodeRecords = new List<SyncReasonCodeRecord>();
        foreach (var site in allSites)
        {
            var siteLines = await lines.ListBySiteAsync(site.Id, cancellationToken);
            lineRecords.AddRange(siteLines.Select(l => new SyncLineRecord(l.Id, l.Name, l.SiteId)));

            var siteReasonCodes = await reasonCodes.ListBySiteAsync(site.Id, cancellationToken);
            reasonCodeRecords.AddRange(siteReasonCodes.Select(r =>
                new SyncReasonCodeRecord(r.Id, r.SiteId, r.Name, r.LossCategory, r.IsActive)));
        }

        var allMachines = await machines.ListByScopeAsync(CallerScope.Global, cancellationToken);
        var machineRecords = allMachines.Select(m => new SyncMachineRecord(m.Id, m.Name, m.LineId)).ToList();

        var closedDowntimeEvents = await downtimeEvents.ListClosedSince(since, cycleStartedAt, cancellationToken);
        var downtimeEventRecords = closedDowntimeEvents
            .Select(e => new SyncDowntimeEventRecord(e.Id, e.MachineId, e.ReasonCodeId, e.StartedAt, e.EndedAt!.Value))
            .ToList();

        var recordedQualityRejects = await qualityRejects.ListRecordedSince(since, cycleStartedAt, cancellationToken);
        var qualityRejectRecords = recordedQualityRejects
            .Select(q => new SyncQualityRejectRecord(q.Id, q.MachineId, q.Quantity, q.RecordedAt))
            .ToList();

        var batch = new SyncBatch(siteRecords, lineRecords, machineRecords, reasonCodeRecords, downtimeEventRecords, qualityRejectRecords);

        var pushed = await syncClient.TryPushAsync(batch, cancellationToken);
        if (pushed)
        {
            // Advance to the captured cycleStartedAt, not a fresh UtcNow read here — a DowntimeEvent
            // closing between the query above and this line would otherwise never satisfy
            // "> since" on any future cycle once the cursor moved past it.
            await cursorStore.SetLastPushedAtAsync(cycleStartedAt, cancellationToken);
        }

        return pushed;
    }
}
