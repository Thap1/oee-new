using OeeNew.Domain.MasterData;

namespace OeeNew.Application.Sync;

/// <summary>
/// Wire contract for the Site → Central sync push (Story 5.1). Both sides of the HTTP call are the
/// identical compiled <c>OeeNew.Api</c>/<c>OeeNew.Application</c> binary (AppMode=Site vs AppMode=Central),
/// so this one shared record type is serialized by <c>HttpSyncClient</c> and deserialized by
/// <c>SyncController</c> with no drift risk. Plain data records, not the Domain entities themselves —
/// never serialize <see cref="Site"/>/<see cref="Line"/>/etc. directly over HTTP.
/// </summary>
public sealed record SyncSiteRecord(Guid Id, string Name);

public sealed record SyncLineRecord(Guid Id, string Name, Guid SiteId);

public sealed record SyncMachineRecord(Guid Id, string Name, Guid LineId);

public sealed record SyncReasonCodeRecord(Guid Id, Guid SiteId, string Name, LossCategory LossCategory, bool IsActive);

public sealed record SyncDowntimeEventRecord(Guid Id, Guid MachineId, Guid? ReasonCodeId, DateTimeOffset StartedAt, DateTimeOffset EndedAt);

public sealed record SyncQualityRejectRecord(Guid Id, Guid MachineId, int Quantity, DateTimeOffset RecordedAt);

public sealed record SyncBatch(
    IReadOnlyList<SyncSiteRecord> Sites,
    IReadOnlyList<SyncLineRecord> Lines,
    IReadOnlyList<SyncMachineRecord> Machines,
    IReadOnlyList<SyncReasonCodeRecord> ReasonCodes,
    IReadOnlyList<SyncDowntimeEventRecord> DowntimeEvents,
    IReadOnlyList<SyncQualityRejectRecord> QualityRejects);
