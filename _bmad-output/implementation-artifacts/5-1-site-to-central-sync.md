---
baseline_commit: f1932b2a23563d7141903e5b8b55da9b8f9ca004
---

# Story 5.1: Đồng bộ dữ liệu site lên trung tâm

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin at Central,
I want each site to periodically sync completed business records to the central instance,
so that I can view cross-site aggregated data without depending on constant site connectivity.

## Acceptance Criteria

1. **Given** site instance hoạt động ở `AppMode=Site` **When** đến chu kỳ đồng bộ **Then** Sync module đẩy lên trung tâm các bản ghi nghiệp vụ đã chốt: DowntimeEvent đã đóng, ProductionCount theo khung giờ, QualityReject — không phải luồng tín hiệu thô (AD-2) `[ASSUMPTION: xem Dev Notes — "ProductionCount" không tồn tại trong codebase, sync thay bằng DowntimeEvent(closed)+QualityReject đúng theo cách OEE report (Story 4.1) đã chọn]`
2. **Given** site mất kết nối tới trung tâm **When** Sync không thực hiện được **Then** site vẫn tiếp tục vận hành đầy đủ (ingestion/dashboard/downtime), dữ liệu chờ đồng bộ ở lần kế tiếp có kết nối (NFR-3)
3. **Given** entity được đồng bộ **When** ghi vào DB trung tâm **Then** dùng cùng khoá `uuidv7()` sinh tại site (AD-6), không đụng độ khoá chính giữa các site
4. **Given** Site/Line/Machine/ReasonCode master data thay đổi tại site **When** đồng bộ **Then** trung tâm cập nhật bản đọc (read-only) tương ứng, không có endpoint nào ở trung tâm ghi ngược lại site (AD-4)

## Tasks / Subtasks

- [ ] Task 1: Resolve AC #1's stale "ProductionCount" wording against what the codebase actually has (AC: #1)
  - [ ] **Read `src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs`'s class doc comment before writing any sync code.** It already states, in Story 4.1's own words: *"there is no ProductionCount/ideal-cycle-time entity anywhere in the codebase... this reuses exactly the same 'seconds lost per LossCategory' data Epic 3's `LossBreakdownQueryUseCase` already established."* `ProductionCount` is a name the Architecture Spine/epics.md coined when Epic 5 was scoped, but Epic 2/3/4 never built it — OEE here is a **time-based-loss proxy** over `DowntimeEvent`+`QualityReject`, confirmed with the user (see memory `epic4_oee_report_decisions`). Do not create a new `ProductionCount` entity/table in this story — that would be new scope nothing in Epic 2-4 asked for and nothing downstream (reports, pie chart) would consume.
  - [ ] `[ASSUMPTION]` this story's sync scope is exactly the two business-record entities that exist and are described as "closed"/"per record" in AD-2: `DowntimeEvent` (only rows with `EndedAt != null` — an open event is not yet a finished business record) and `QualityReject` (every row — it's already append-only, Story 2.6 Dev Notes). Plus, per AD-4, the master data these two reference: `Site`, `Line`, `Machine`, `ReasonCode`.
- [ ] Task 2: Site-side — sync cursor storage, so a push cycle knows what's new since last success (AC: #1, #2)
  - [ ] New Domain-free table `SyncCursor` (single row, `Id smallint PRIMARY KEY DEFAULT 1`, `LastPushedAt timestamptz NULL`) — deliberately a plain infrastructure/runtime-state table, not a Domain entity (same reasoning `MachineState` vs `Machine` already split runtime state from master-data identity). Map directly via EF Core in `OeeDbContext` as a simple class `SyncCursorRow { public short Id; public DateTimeOffset? LastPushedAt; }` — no rich Domain type needed, this is pure infrastructure bookkeeping, not a business rule–bearing entity.
  - [ ] New file `src/OeeNew.Application/Sync/ISyncCursorStore.cs`:
    ```csharp
    public interface ISyncCursorStore
    {
        Task<DateTimeOffset?> GetLastPushedAtAsync(CancellationToken cancellationToken = default);
        Task SetLastPushedAtAsync(DateTimeOffset value, CancellationToken cancellationToken = default);
    }
    ```
  - [ ] New file `src/OeeNew.Infrastructure/Persistence/SyncCursorStore.cs` implementing it against `OeeDbContext` (upsert the single row by `Id = 1`).
  - [ ] **Why a single-row cursor instead of a "SyncedAt" flag column on `DowntimeEvent`/`QualityReject`:** adding a synced-marker column to those two tables would touch Epic 2's hot ingestion path and its existing EF mappings/tests for no real benefit — a single timestamp cursor plus an idempotent upsert at Central (Task 6) gives the same at-least-once-delivery safety with a much smaller footprint. `[ASSUMPTION]`, since the Architecture Spine's Deferred section explicitly left "cơ chế Sync module" unspecified.
- [ ] Task 3: Site-side — gather-and-push use case (AC: #1, #2, #3)
  - [ ] New file `src/OeeNew.Application/Sync/SyncBatch.cs` — the wire contract shared by both sides of the sync HTTP call (Site serializes it, Central deserializes the identical shape — same binary, no drift possible):
    ```csharp
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
    ```
    Plain data records in `Application` (same pattern as `ClosedDowntimeSlice` — a read-only projection type Application defines and both Infrastructure and Api reference), not the actual Domain entities — never serialize `Domain.MasterData.Site` etc. directly over HTTP.
  - [ ] `[ASSUMPTION]` **master data is resent as a full current snapshot every cycle, not diffed.** `Site`/`Line`/`Machine`/`ReasonCode` are small tables (tens to low hundreds of rows for a real factory) — resending everything each cycle is cheap and sidesteps needing change-tracking (`UpdatedAt` columns, dirty flags) on four tables that Epic 1 never designed for it. Only `DowntimeEvent`/`QualityReject` use the cursor (Task 2) to send just what's new, since those can grow unbounded.
  - [ ] New file `src/OeeNew.Application/Sync/ISyncClient.cs` (name matches the Architecture Spine's source-tree comment: *"interface cho Infrastructure (IProductionDataSource, ISyncClient...)"*):
    ```csharp
    public interface ISyncClient
    {
        /// <summary>Returns false (not thrown) on any transport/HTTP failure — Task 4 treats that as "try again next cycle," not an error to propagate (AC #2).</summary>
        Task<bool> TryPushAsync(SyncBatch batch, CancellationToken cancellationToken = default);
    }
    ```
  - [ ] New file `src/OeeNew.Application/Sync/PushSyncBatchUseCase.cs`, constructor `(ISiteRepository sites, ILineRepository lines, IMachineRepository machines, IReasonCodeRepository reasonCodes, IDowntimeEventRepository downtimeEvents, IQualityRejectRepository qualityRejects, ISyncClient syncClient, ISyncCursorStore cursorStore)`:
    ```csharp
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var cycleStartedAt = DateTimeOffset.UtcNow;
        var since = await cursorStore.GetLastPushedAtAsync(cancellationToken) ?? DateTimeOffset.MinValue;

        var batch = new SyncBatch(
            (await sites.ListAsync(cancellationToken)).Select(s => new SyncSiteRecord(s.Id, s.Name)).ToList(),
            /* lines/machines/reasonCodes: same shape, ListAsync()-style "everything in this local DB" per Task 3's full-snapshot decision */
            ...,
            await downtimeEvents.ListClosedSince(since, cycleStartedAt, cancellationToken),   // Task 5 — new repository method
            await qualityRejects.ListRecordedSince(since, cycleStartedAt, cancellationToken)); // Task 5 — new repository method

        var pushed = await syncClient.TryPushAsync(batch, cancellationToken);
        if (pushed)
        {
            await cursorStore.SetLastPushedAtAsync(cycleStartedAt, cancellationToken);
        }
        return pushed;
    }
    ```
    **Capture `cycleStartedAt` before querying, and advance the cursor to that captured value (not `DateTimeOffset.UtcNow` read again after the push)** — otherwise a `DowntimeEvent` that closes between the query and the cursor update would be silently skipped forever (never `>= since` on any future cycle once the cursor moves past it).
  - [ ] `ILineRepository`/`IMachineRepository`/`IReasonCodeRepository` already have a `ListAsync`/`ListByScopeAsync`-style "everything" method (mirror whichever one `SiteRepository.ListAsync()` (`src/OeeNew.Infrastructure/Persistence/SiteRepository.cs`) already establishes as the "no scope filter, just all rows in this local DB" pattern) — reuse them, don't add new query methods for the full-snapshot reads.
- [ ] Task 4: Site-side — periodic background push, opt-in like `DemoSignalSimulatorHostedService` (AC: #1, #2)
  - [ ] New file `src/OeeNew.Infrastructure/Sync/SyncOptions.cs`: `public sealed class SyncOptions { public const string SectionName = "Sync"; public bool Enabled { get; set; } public string? CentralBaseUrl { get; set; } public string? ApiKey { get; set; } public int IntervalSeconds { get; set; } = 60; }` — same options-class shape as `ProductionOptions`/`JwtOptions`.
  - [ ] New file `src/OeeNew.Infrastructure/Sync/SyncPushHostedService.cs` — copy `DemoSignalSimulatorHostedService`'s exact shape (`BackgroundService`, `IServiceScopeFactory` + `PeriodicTimer`, one scope per tick, resolve `PushSyncBatchUseCase` inside the scope): on each tick call `RunOnceAsync`; **log and continue on any exception/false return — never let a failed push crash the loop** (this is the concrete mechanism behind AC #2's "site vẫn tiếp tục vận hành đầy đủ").
  - [ ] `src/OeeNew.Api/Program.cs`: `builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));` alongside the other `Configure<...Options>` calls (line ~45). Register `SyncPushHostedService` **only when both `appMode == "Site"` and `Sync:Enabled` is true**:
    ```csharp
    if (appMode == "Site" && builder.Configuration.GetValue<bool>("Sync:Enabled"))
    {
        builder.Services.AddHttpClient<ISyncClient, HttpSyncClient>();
        builder.Services.AddScoped<PushSyncBatchUseCase>();
        builder.Services.AddScoped<ISyncCursorStore, SyncCursorStore>();
        builder.Services.AddHostedService<SyncPushHostedService>();
    }
    ```
    Opt-in (default `false`) for the same reason `Production:SimulateSignal` is opt-in: most dev/demo runs are a single standalone instance with no Central counterpart reachable — don't spin up a background loop that immediately fails every tick against a placeholder `CentralBaseUrl`.
  - [ ] New file `src/OeeNew.Infrastructure/Sync/HttpSyncClient.cs` implementing `ISyncClient` via injected `HttpClient` (base address = `SyncOptions.CentralBaseUrl`, `IOptions<SyncOptions>` for the API key): `POST {CentralBaseUrl}/api/sync/batch` with header `X-Sync-Api-Key: {ApiKey}` and the `SyncBatch` as JSON body. Catch `HttpRequestException`/non-2xx and return `false` (AC #2 — never throw out of this method).
- [ ] Task 5: Site-side repository extensions — "what changed since the cursor" (AC: #1)
  - [ ] `src/OeeNew.Application/Production/IDowntimeEventRepository.cs`: add
    ```csharp
    /// <summary>Closed events (EndedAt != null) whose EndedAt falls in (since, asOf] — the Sync module's "what's new" query (Story 5.1). Unlike ListClosedSlicesInRangeAsync, returns full entities (ReasonCodeId/StartedAt/EndedAt all needed on the wire), not the ClosedDowntimeSlice projection, and is NOT scoped to a machine list — sync pushes everything this local DB has.</summary>
    Task<IReadOnlyList<DowntimeEvent>> ListClosedSince(DateTimeOffset since, DateTimeOffset asOf, CancellationToken cancellationToken = default);
    ```
    Implement in `DowntimeEventRepository.cs` filtering `e.EndedAt != null && e.EndedAt > since && e.EndedAt <= asOf`.
  - [ ] `src/OeeNew.Application/Production/IQualityRejectRepository.cs`: add `Task<IReadOnlyList<QualityReject>> ListRecordedSince(DateTimeOffset since, DateTimeOffset asOf, CancellationToken cancellationToken = default)`, filtering `RecordedAt > since && RecordedAt <= asOf`.
  - [ ] Update both fake test doubles (`tests/OeeNew.Application.Tests/Production/FakeDowntimeEventRepository.cs`, `FakeQualityRejectRepository.cs`) with the new methods.
- [ ] Task 6: Central-side — receive endpoint, idempotent upsert, AppMode guard (AC: #3, #4)
  - [ ] New file `src/OeeNew.Application/Sync/ISyncIngestRepository.cs`: `Task IngestAsync(SyncBatch batch, CancellationToken cancellationToken = default);` — one call, one DB transaction.
  - [ ] New file `src/OeeNew.Application/Sync/ISyncStatusRepository.cs`: `Task RecordSyncedAsync(Guid siteId, DateTimeOffset syncedAt, CancellationToken cancellationToken = default);` (read side — listing statuses for the badge — is Story 5.3's concern; this story only needs the write path that a successful ingest calls).
  - [ ] New file `src/OeeNew.Application/Sync/ReceiveSyncBatchUseCase.cs`: `IngestAsync(SyncBatch batch, ct)` → calls `ISyncIngestRepository.IngestAsync` then, **for every `SyncSiteRecord` in the batch** (not a single "calling site id" — see Dev Notes on why), `ISyncStatusRepository.RecordSyncedAsync(site.Id, DateTimeOffset.UtcNow, ct)`.
  - [ ] New file `src/OeeNew.Infrastructure/Persistence/SyncIngestRepository.cs` implementing `ISyncIngestRepository` against `OeeDbContext`, in FK-dependency order (Site → Line → Machine → ReasonCode → DowntimeEvent/QualityReject) inside one `context.Database.BeginTransactionAsync()`:
    - For each master-data record: `var existing = await context.Sites.FindAsync([id], ct);` — if found, call the entity's existing mutator (`Site.Rename(name)`, `Line.Rename(name)`, `Machine.Rename(name)`; for `ReasonCode`, only `Name` and `Deactivate()`-when-`!IsActive` are applied — see Dev Notes' known gap) and let `SaveChanges` emit the `UPDATE`; if not found, `Add(new Site(id, name))` (same pattern for Line/Machine/ReasonCode's constructors).
    - **Trust EF Core's existing `ValueGeneratedOnAdd()` + `HasDefaultValueSql("uuidv7()")` configuration (`OeeDbContext.cs` — same config already in place for every synced entity) to preserve the client-supplied `Id` on `Add`**: that configuration only asks Postgres to generate a value when the CLR property is left at its type default (`Guid.Empty`); since every incoming record already carries a real, non-empty `Id` minted at the origin site (AD-6), a plain `Add(new Site(id, name))` inserts with that exact `Id` — do not bypass this with raw SQL upserts, and do not "fix" the mapping thinking it will silently regenerate the key.
    - For `DowntimeEvent`/`QualityReject`: **insert-only, skip if the `Id` already exists** (`await context.DowntimeEvents.AnyAsync(d => d.Id == record.Id, ct)`) — these are immutable closed business records once synced, so re-delivery after a retried push (AC #2's failure-and-retry path) must be a no-op, not a duplicate or an error. Reconstruct via the existing Domain API, don't add new constructors: `new DowntimeEvent(record.Id, record.MachineId, record.StartedAt)`, then `.AssignReason(reasonCodeId)` if present (call before `.Close`, since `AssignReason` throws once `!IsOpen`), then `.Close(record.EndedAt)`.
  - [ ] New file `src/OeeNew.Infrastructure/Persistence/SyncStatusRepository.cs` implementing `ISyncStatusRepository` — upsert a `SiteSyncStatus(SiteId, LastSyncedAt)` row (same find-or-add shape as the master-data upserts above).
  - [ ] New file `src/OeeNew.Api/Sync/ApiKeyAuthFilter.cs` — a plain `IAsyncActionFilter` (not a new ASP.NET authentication scheme; the existing JWT Bearer scheme stays the sole `[Authorize]`-based mechanism for user-facing endpoints) that reads the `X-Sync-Api-Key` header and compares it (constant-time, `CryptographicOperations.FixedTimeEquals`) against `IOptions<SyncOptions>.Value.ApiKey`; short-circuits with the standard `{ code: "UNAUTHORIZED", message }` envelope (`ApiErrorWriter`, matching every other 401 path in this codebase) on mismatch or missing header.
  - [ ] New file `src/OeeNew.Api/Controllers/SyncController.cs`:
    ```csharp
    [ApiController]
    [Route("api/sync")]
    [AllowAnonymous] // no JWT on this endpoint at all — ApiKeyAuthFilter is the only gate
    [ServiceFilter(typeof(ApiKeyAuthFilter))]
    public sealed class SyncController(ReceiveSyncBatchUseCase useCase, AppModeInfo appMode) : ControllerBase
    {
        [HttpPost("batch")]
        public async Task<IActionResult> ReceiveBatch([FromBody] SyncBatch batch, CancellationToken cancellationToken)
        {
            if (appMode.Mode != "Central")
            {
                return NotFound(); // this instance isn't a Central instance — never silently accept a payload it has no reason to store
            }
            await useCase.IngestAsync(batch, cancellationToken);
            return NoContent();
        }
    }
    ```
    `AppModeInfo` is the existing singleton from `Program.cs:41` (`public sealed record AppModeInfo(string Mode)`) — reuse it directly, don't add a second mode-check mechanism.
  - [ ] `Program.cs`: register `builder.Services.AddScoped<ISyncIngestRepository, SyncIngestRepository>(); builder.Services.AddScoped<ISyncStatusRepository, SyncStatusRepository>(); builder.Services.AddScoped<ReceiveSyncBatchUseCase>(); builder.Services.AddScoped<ApiKeyAuthFilter>();` unconditionally (both AppModes can have the controller mapped; the `appMode.Mode != "Central"` runtime check in the action is the actual guard, matching how this codebase already prefers a runtime check over conditional DI registration for cross-cutting AppMode behavior — see the existing `AppModeInfo` singleton being always registered regardless of mode).
- [ ] Task 7: EF Core migration + schema regeneration (AC: #3, #4)
  - [ ] `dotnet ef migrations add AddSyncTables --project src/OeeNew.Infrastructure --startup-project src/OeeNew.Api` — adds `SyncCursor` (`Id smallint PK default 1`, `LastPushedAt timestamptz NULL`) and `SiteSyncStatus` (`SiteId uuid PK`, `LastSyncedAt timestamptz NOT NULL`, FK to `Site(Id)` `ON DELETE RESTRICT` — same restrict convention as every other FK in `OeeDbContext.cs`). Both tables exist in the one shared `OeeDbContext`/migration history regardless of `AppMode` (a Site instance's DB has an always-empty `SiteSyncStatus`, Central's has an always-empty `SyncCursor` — consistent with "same schema, different active modules per AppMode" already established for this whole codebase).
  - [ ] Regenerate `db/init/01_schema.sql`: `dotnet ef migrations script --idempotent -o db/init/01_schema.sql --project src/OeeNew.Infrastructure --startup-project src/OeeNew.Api` (same command Story `spec-demo-seed-data` used — never hand-edit this file).
  - [ ] `db/init/02_seed.sql`: no changes needed — `SyncCursor`/`SiteSyncStatus` start empty; both AppModes' demo seed data works unchanged.
  - [ ] Add `"Sync": { "Enabled": false }` to `src/OeeNew.Api/appsettings.json` (mirrors `Production:SimulateSignal`'s off-by-default pattern) — leave `CentralBaseUrl`/`ApiKey`/`IntervalSeconds` unset/default so a real deployment must explicitly configure them.
- [ ] Task 8: Testing (all AC)
  - [ ] New file `tests/OeeNew.Application.Tests/Sync/PushSyncBatchUseCaseTests.cs`: first run with no prior cursor pushes everything currently in the fake repositories; a second run after a successful push only includes a `DowntimeEvent` closed after the first run's `cycleStartedAt` (proves the cursor advances correctly and doesn't re-send already-pushed rows); `TryPushAsync` returning `false` leaves the cursor unchanged (AC #2 — nothing is marked as sent when the push itself failed).
  - [ ] New file `tests/OeeNew.Application.Tests/Sync/ReceiveSyncBatchUseCaseTests.cs` (or a repository-level test against a fake `ISyncIngestRepository`/real EF Core via the existing Postgres test-fixture pattern, matching how `tests/OeeNew.Api.Tests/**/*EndpointsTests.cs` already do full-flow assertions): ingesting the same batch twice produces no duplicate `DowntimeEvent`/`QualityReject` rows and no error (AC #3 idempotency); a `Site`/`Line`/`Machine`/`ReasonCode` whose `Name` changed between two batches is updated in place, not duplicated (AC #4); a `DowntimeEvent` with `ReasonCodeId == null` (unattributed, Story 2.5 Dev Notes) round-trips correctly.
  - [ ] New file `tests/OeeNew.Api.Tests/Sync/SyncEndpointsTests.cs`, real-Postgres full-flow style (matching `LossAnalyticsEndpointsTests.cs`): a request with a correct `X-Sync-Api-Key` against a `Central`-mode test host succeeds (204) and the batch's rows land in the DB; a wrong/missing key gets 401; a request against a `Site`-mode test host gets 404 regardless of the key (the `appMode.Mode != "Central"` guard); after a successful ingest, `SiteSyncStatus.LastSyncedAt` for each synced `Site.Id` is set to (approximately) now.
  - [ ] New file `tests/OeeNew.Infrastructure.Tests`-equivalent-or-inline: since there's no separate Infrastructure test project in this repo today (confirmed — only `Domain.Tests`/`Application.Tests`/`Architecture.Tests`/`Api.Tests` exist), cover `SyncCursorStore`/`SyncIngestRepository`/`SyncStatusRepository` through the `Api.Tests` real-Postgres endpoint tests above rather than inventing a fifth test project for this story alone.

## Dev Notes

- **The single biggest risk in this story is silently inventing a `ProductionCount` entity because AC #1 still names it.** Don't. Re-read `OeeReportQueryUseCase`'s doc comment (Story 4.1) before starting — the whole codebase already committed to a time-based-loss-proxy OEE built on `DowntimeEvent`+`QualityReject` alone, confirmed with the user (memory: `epic4_oee_report_decisions`). A "ProductionCount" table would be new, unused-by-everything-else scope this story has no business adding.
- **Same-binary-both-sides is why `SyncBatch` can be one shared record type.** `AppMode=Site` and `AppMode=Central` run the identical compiled `OeeNew.Api`/`OeeNew.Application` — there's no separate "client SDK" vs "server contract" drift risk here the way there would be across genuinely different codebases. Lean into that: define the wire DTOs once in `Application/Sync/`, reference them from both the push side (Infrastructure `HttpSyncClient`) and the receive side (Api `SyncController`).
- **Because the same schema already backs both AppModes, every existing dashboard/report/pie-chart query "just works" against Central's DB once this story lands data in it — that's the entire point, and Story 5.2 should need close to zero backend changes as a result.** Don't add Central-specific read endpoints in this story; this story is write-path only (receiving + upserting), consistent with its AC set (#1, #3, #4 are all about the data landing correctly, none about querying it back).
- **API-key auth, not a reused JWT, and that's a deliberate simplification, not an oversight.** A Site's push loop is a headless background service with no interactive login and no natural "user" — reusing the human/JWT Identity Provider (AD-7) for this would mean minting a fake machine `User` row and teaching every `AdminOnly`/`ReportsAccess`-style screen to reason about a synthetic account, plus token-refresh logic a background loop doesn't need. A single shared secret validated by a plain `IAsyncActionFilter` is the smaller, more legible mechanism for this specific machine-to-machine call — the Architecture Spine's own Deferred section left "cơ chế Sync module" open precisely for a call like this. If per-site key rotation/revocation becomes a real requirement later, that's an incremental change to `ApiKeyAuthFilter`, not a redesign.
- **`SiteSyncStatus` is recorded per `Site.Id` found *in the batch*, not per "calling instance."** This matters because the current demo/deploy seed data (`spec-demo-seed-data.md`) puts two `Site` rows in one shared database for demo convenience — the real per-site-database production topology (Architecture Spine's Deployment section) is 1:1, but this story's ingestion logic shouldn't assume that 1:1 mapping holds for every environment. Keying the sync-status badge off "which `Site` rows actually arrived in this payload" is correct in both cases.
- **Known, accepted gap: `ReasonCode` reactivation and `LossCategory` changes don't sync.** `ReasonCode.Deactivate()` is the only mutator the Domain model exposes (Story 1.5) — there's no `Activate()` and no way to change `LossCategory` post-creation. If a site reactivates a deactivated reason code or (very unusually) needed to fix a miscategorized one, Central's read-only copy won't reflect it until this gap is addressed in a later story. This is a deliberate scope boundary, not a bug to chase down now — don't add new Domain mutators speculatively to close it.
- **Retry safety end-to-end:** the site-side cursor only advances after `TryPushAsync` returns `true` (Task 3); the central-side ingest is idempotent by `Id`-existence-check for `DowntimeEvent`/`QualityReject` and by find-or-update for master data (Task 6). Together these mean a crash/timeout mid-push is always safe to retry from scratch next tick — never assume "the HTTP call returned, so it must have landed," and never assume "it's already landed, so skip resending."

### Project Structure Notes

- New Application folder `src/OeeNew.Application/Sync/`: `SyncBatch.cs`, `ISyncClient.cs`, `ISyncCursorStore.cs`, `PushSyncBatchUseCase.cs`, `ISyncIngestRepository.cs`, `ISyncStatusRepository.cs`, `ReceiveSyncBatchUseCase.cs` — mirrors the existing `Analytics/`/`Reports/` folder-per-capability convention.
- New Infrastructure folder `src/OeeNew.Infrastructure/Sync/`: `SyncOptions.cs`, `SyncPushHostedService.cs`, `HttpSyncClient.cs`. New Persistence files: `SyncCursorStore.cs`, `SyncIngestRepository.cs`, `SyncStatusRepository.cs`.
- New Api files: `src/OeeNew.Api/Controllers/SyncController.cs`, `src/OeeNew.Api/Sync/ApiKeyAuthFilter.cs`.
- `OeeDbContext.cs`: two new `DbSet`s (`SyncCursors`? — actually a single conceptual row, consider exposing as `DbSet<SyncCursorRow> SyncCursor` for EF Core plumbing even though only one row is ever expected) and `DbSet<SiteSyncStatus> SiteSyncStatuses`, plus their `OnModelCreating` mappings.
- New EF Core migration `AddSyncTables`; regenerated `db/init/01_schema.sql`; `db/init/02_seed.sql` untouched.
- No changes to `Domain` — `DowntimeEvent`/`QualityReject`/`Site`/`Line`/`Machine`/`ReasonCode` are reused via their existing public constructors/mutators exactly as-is.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-5] — Story 5.1 full AC, including the "ProductionCount" wording this story's Task 1 resolves against actual codebase state
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md] — AD-2 (site autonomy, what gets synced), AD-4 (master data ownership, read-only at Central), AD-6 (uuidv7 identity), Deferred section ("cơ chế/tần suất chính xác của Sync module... chưa quyết định" — this story makes that decision: REST push, opt-in interval, single shared API key)
- [Source: src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs] — authoritative confirmation that no `ProductionCount` entity exists; the time-based-loss-proxy OEE decision this story's Task 1 defers to
- [Source: src/OeeNew.Infrastructure/Production/DemoSignalSimulatorHostedService.cs] — the exact opt-in-config + `BackgroundService`/`PeriodicTimer`/`IServiceScopeFactory` shape `SyncPushHostedService` copies
- [Source: src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs] — existing `uuidv7()`/`ValueGeneratedOnAdd()` FK/table conventions every new mapping in this story follows exactly; confirms the "client-supplied Guid survives Add()" behavior this story's ingest repository relies on
- [Source: src/OeeNew.Application/Production/IDowntimeEventRepository.cs, IQualityRejectRepository.cs] — existing repository shape/conventions the new `ListClosedSince`/`ListRecordedSince` methods extend
- [Source: src/OeeNew.Domain/Production/DowntimeEvent.cs, QualityReject.cs] — existing constructors/mutators (`AssignReason`, `Close`) reused as-is to reconstruct synced records; no Domain changes
- [Source: src/OeeNew.Domain/MasterData/Site.cs, Line.cs, Machine.cs, ReasonCode.cs] — existing `Rename`/`Deactivate` mutators reused for master-data upsert; `ReasonCode`'s missing `Activate()`/category-change mutator is this story's documented known gap
- [Source: src/OeeNew.Api/Program.cs] — existing `AppModeInfo` singleton (line 41) reused as the Central-only guard for `SyncController`; existing `Configure<...Options>`/conditional-hosted-service registration patterns (`Production:SimulateSignal`) mirrored for `Sync:Enabled`
- [Source: _bmad-output/implementation-artifacts/spec-demo-seed-data.md] — confirms the demo/deploy seed's two-sites-in-one-database reality, the reason `SiteSyncStatus` keys off batch contents rather than a single "calling site" identity
- [Source: _bmad-output/implementation-artifacts/4-1-oee-report-shift-day-week.md] — first place in the codebase to flag "no ProductionCount entity exists," directly reused by this story's Task 1

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
