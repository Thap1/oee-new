---
baseline_commit: f1932b2a23563d7141903e5b8b55da9b8f9ca004
---

# Story 5.3: Hiển thị trạng thái đồng bộ (Sync Badge)

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin/Manager viewing cross-site data,
I want to see "last synced X minutes ago" per site,
so that I don't mistake a quiet site for a site that's down.

## Acceptance Criteria

1. **Given** tôi xem Dashboard/Report tại Central **When** dữ liệu của một site hiển thị **Then** kèm badge "Đồng bộ lần cuối: X phút trước" dựa trên last-sync-timestamp của site đó (UX-DR11)
2. **Given** thời gian từ lần đồng bộ cuối vượt ngưỡng cấu hình **When** badge hiển thị **Then** chuyển màu cảnh báo — nhưng không phải lỗi, chỉ là thông tin (UX-DR11)
3. **Given** Admin xem badge cảnh báo **When** điều tra **Then** phân biệt được đây là vấn đề đồng bộ (kết nối liên site) chứ không phải máy hỏng tại site đó

## Tasks / Subtasks

- [ ] Task 1: Backend — read side for `SiteSyncStatus` (the write side already exists from Story 5.1) (AC: #1)
  - [ ] **Story 5.1 already created the table and the write path** (`SiteSyncStatus(SiteId, LastSyncedAt)`, upserted by `ISyncStatusRepository.RecordSyncedAsync` every time `ReceiveSyncBatchUseCase` ingests a batch — see `src/OeeNew.Application/Sync/ISyncStatusRepository.cs`). This story only adds the **read** path — do not re-create the table or duplicate the write logic.
  - [ ] `src/OeeNew.Application/Sync/ISyncStatusRepository.cs`: add
    ```csharp
    public sealed record SiteSyncStatusRecord(Guid SiteId, DateTimeOffset LastSyncedAt);

    Task<IReadOnlyList<SiteSyncStatusRecord>> ListAllAsync(CancellationToken cancellationToken = default);
    ```
    Implement in `src/OeeNew.Infrastructure/Persistence/SyncStatusRepository.cs` (Story 5.1) — a plain `ToListAsync()` projection over the `SiteSyncStatus` table, no filtering (scope filtering happens one layer up, same pattern as `SiteManagementUseCase.ListAsync`).
- [ ] Task 2: Backend — configurable staleness threshold (AC: #2)
  - [ ] Extend `src/OeeNew.Infrastructure/Sync/SyncOptions.cs` (Story 5.1) with one more field: `public int WarningThresholdMinutes { get; set; } = 15;` — reuse the existing shared `Sync` config section rather than adding a second options class for one number; Site instances simply never read this field (same as Central never reading `IntervalSeconds`/`CentralBaseUrl` today).
  - [ ] Add `"Sync": { "WarningThresholdMinutes": 15 }` alongside the existing `"Sync": { "Enabled": false }` entry in `src/OeeNew.Api/appsettings.json` (merge into the same `Sync` object, don't duplicate the section).
- [ ] Task 3: Backend — `SyncStatusQueryUseCase`: one row per accessible Site, "never synced" included (AC: #1, #2)
  - [ ] **Must enumerate from `Site`, not from `SiteSyncStatus`.** A site the caller can see but that has never completed a sync has no `SiteSyncStatus` row at all — omitting it (by only iterating existing status rows) would silently hide exactly the case AC #2/#3 most need to surface (a site that's *never* synced is at least as noteworthy as one that's merely gone stale). Left-join in application code: start from every `Site` in `CallerScope`, then look up a matching `SiteSyncStatusRecord` by `SiteId`, defaulting to "never synced" (`LastSyncedAt: null`) when absent.
  - [ ] New file `src/OeeNew.Application/Sync/SyncStatusQueryUseCase.cs`, constructor `(ISiteRepository sites, ISyncStatusRepository syncStatuses, IOptions<SyncOptions> options)`:
    ```csharp
    public sealed record SiteSyncStatusResult(Guid SiteId, string SiteName, DateTimeOffset? LastSyncedAt, bool IsStale);

    public async Task<IReadOnlyList<SiteSyncStatusResult>> GetStatusesAsync(CallerScope scope, CancellationToken cancellationToken = default)
    {
        var allSites = await sites.ListAsync(cancellationToken); // SiteManagementUseCase.ListAsync's own "everything, filter by scope" shape (Story 1.2/1.6) — same pattern, don't re-derive it differently here
        var scopedSites = scope.IsGlobal ? allSites : allSites.Where(s => scope.AllowsSite(s.Id)).ToList();

        var statusesBySite = (await syncStatuses.ListAllAsync(cancellationToken)).ToDictionary(s => s.SiteId, s => s.LastSyncedAt);
        var threshold = TimeSpan.FromMinutes(options.Value.WarningThresholdMinutes);
        var now = DateTimeOffset.UtcNow;

        return scopedSites.Select(site =>
        {
            var lastSyncedAt = statusesBySite.GetValueOrDefault(site.Id) is { } value ? value : (DateTimeOffset?)null;
            var isStale = lastSyncedAt is null || now - lastSyncedAt.Value > threshold; // AC #2 — null (never synced) always counts as stale
            return new SiteSyncStatusResult(site.Id, site.Name, lastSyncedAt, isStale);
        }).ToList();
    }
    ```
- [ ] Task 4: Backend — `GET /api/sync/status`, a normal JWT-authenticated endpoint, deliberately **not** sharing `SyncController`'s API-key gate (AC: #1)
  - [ ] **Do not add this action to Story 5.1's `SyncController`.** That controller is `[AllowAnonymous] [ServiceFilter(typeof(ApiKeyAuthFilter))]` at the class level for the machine-to-machine batch-receive endpoint — mixing an `[Authorize]`-protected, human-facing read action into the same class risks the class-level `[AllowAnonymous]` silently short-circuiting authorization for the new action too (ASP.NET Core unions endpoint metadata; a class-level `[AllowAnonymous]` isn't safely overridden by an action-level `[Authorize]`). This is a genuinely different caller (a logged-in Admin/Manager, not a site's push loop) and belongs in its own controller.
  - [ ] New file `src/OeeNew.Api/Controllers/SyncStatusController.cs`:
    ```csharp
    public sealed record SiteSyncStatusResponse(Guid SiteId, string SiteName, DateTimeOffset? LastSyncedAt, bool IsStale);

    [ApiController]
    [Route("api/sync/status")]
    [Authorize(Policy = "ReportsAccess")] // Admin/Manager/Viewer — same set Reports already restricts to (Story 4.1); Operators have no reason to see cross-site sync state
    public sealed class SyncStatusController(SyncStatusQueryUseCase useCase) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SiteSyncStatusResponse>>> Get(CancellationToken cancellationToken)
        {
            var statuses = await useCase.GetStatusesAsync(User.GetCallerScope(), cancellationToken);
            return Ok(statuses.Select(s => new SiteSyncStatusResponse(s.SiteId, s.SiteName, s.LastSyncedAt, s.IsStale)).ToList());
        }
    }
    ```
  - [ ] **No `AppMode` gate on this read endpoint, unlike `SyncController`'s hard `404` at non-Central (Story 5.1/5.2's write-path convention).** At a Site instance, `SiteSyncStatus` is always empty by construction (Story 5.1 Dev Notes), so this naturally returns every accessible site with `lastSyncedAt: null, isStale: true` — harmless, self-explanatory, and not worth a special-cased block for a plain `GET`. The frontend (Task 6) only calls/renders this at Central anyway.
  - [ ] `Program.cs`: register `builder.Services.AddScoped<SyncStatusQueryUseCase>();` alongside the other Sync registrations from Story 5.1.
- [ ] Task 5: Testing — backend (AC: #1, #2, #3)
  - [ ] New file `tests/OeeNew.Application.Tests/Sync/SyncStatusQueryUseCaseTests.cs`: a site with a recent `SiteSyncStatus` row → `IsStale == false`; a site whose `LastSyncedAt` is older than `WarningThresholdMinutes` → `IsStale == true`; a site with **no** `SiteSyncStatus` row at all still appears in the result with `LastSyncedAt: null, IsStale: true` (the "never synced" case, AC #1/#2's most important edge case); a Manager scoped to Site A only never sees Site B's status row, even if Site B is stale (scope enforcement, consistent with every other master-data query).
  - [ ] New file `tests/OeeNew.Api.Tests/Sync/SyncStatusEndpointTests.cs`, real-Postgres full-flow style: an Operator token gets 403 from `GET /api/sync/status` (`ReportsAccess` policy); a Manager/Admin token gets 200 with the correct per-site shape; a request with no `Authorization` header gets 401 (confirms this endpoint is NOT accidentally anonymous the way `SyncController`'s batch endpoint is — the exact landmine Task 4 calls out).
- [ ] Task 6: Frontend — `SyncStatusPanel`, shown on Central-mode Dashboard and Reports (AC: #1, #2, #3)
  - [ ] New file `web/oee-shell/src/app/shared/sync-status/sync-status.service.ts` (or `core/sync-status/` — match whichever convention `AppModeService`, Story 5.2, ends up living under): `getStatuses(): Promise<SiteSyncStatusDto[]>` calling `GET /api/sync/status`, same `firstValueFrom(this.http.get<T>(...))` shape as every other service in this app (`loss-analytics.service.ts`, `oee-report.service.ts`).
  - [ ] New file `web/oee-shell/src/app/shared/sync-status/sync-status-panel.ts`, a small self-contained widget (own `ngOnInit` fetch, own signals) — **not** parameterized by a single site, since AC #1 asks for a per-site badge across however many sites the caller can see at once, not a single-site lookup:
    ```html
    @for (status of statuses(); track status.siteId) {
      <span class="sync-badge" [class.sync-badge--stale]="status.isStale" data-testid="sync-badge">
        <i class="pi pi-sync" aria-hidden="true"></i>
        <span class="sync-badge__site">{{ status.siteName }}</span>
        <span class="sync-badge__text">{{ relativeTimeLabel(status) | translate: relativeTimeParams(status) }}</span>
      </span>
    }
    ```
    `relativeTimeLabel`/`relativeTimeParams` return either `sync.badge.neverSynced` (no params) when `lastSyncedAt` is `null`, or `sync.badge.lastSynced` with `{ minutes: <elapsed> }` otherwise — elapsed computed client-side from `lastSyncedAt` at render time (AC #2 doesn't require live-ticking updates the way the no-signal card's `ClockTickService` does; a value computed once when the panel loads is enough, since re-opening/refreshing the page is the expected way this gets updated, matching AD-8's "no real-time expectation at Central").
  - [ ] **Deliberately distinct icon and color from the Machine Status Card's no-signal state (AC #3's actual requirement).** `MachineStatusCard`'s no-signal variant uses icon `pi-ban` and color `--status-no-signal` (gray) — reusing either here would visually conflate "this site's sync is stale" with "this specific machine has no signal," exactly the confusion AC #3 asks to avoid. Use icon `pi-sync` (already used above) and a new, separate CSS custom property `--sync-badge-stale` (define as the same amber `#F59E0B` `--status-idle` already uses for "needs attention, not an error" — reusing the same *color value* for consistent visual language, but as its own named token so nothing in code reads `sync-badge--idle` and gets confused with machine Idle state). Normal (non-stale) badges use a neutral/muted style — no new token needed, reuse existing muted-text styling already present elsewhere in the app (no dedicated "success" color required here; the badge is informational by default per AC #2, not a status indicator needing its own green).
  - [ ] i18n: add `sync.badge.lastSynced` (`"Đồng bộ lần cuối: {{minutes}} phút trước"` / `"Last synced {{minutes}} min ago"`), `sync.badge.neverSynced` (`"Chưa đồng bộ lần nào"` / `"Never synced"`) to `en.json`/`vi.json` under a new top-level `sync` namespace.
- [ ] Task 7: Frontend — wire `SyncStatusPanel` into Dashboard and Reports, Central-mode only (AC: #1)
  - [ ] `web/oee-shell/src/app/pages/dashboard/dashboard-page.ts` (Story 5.2 already added the `AppModeService`/`isCentral()`-gated Central view here — this story extends that same branch, doesn't add a new one): render `<app-sync-status-panel>` above the `<app-loss-pie-chart>` when `appMode.isCentral()`.
  - [ ] `web/oee-shell/src/app/pages/reports/reports-page.ts`: inject `AppModeService`; render `<app-sync-status-panel>` above the report content when `appMode.isCentral()` is `true` (Reports has no existing Central/Site branch yet — this is the first one, so add the same `inject(AppModeService)` + `computed`/signal-read pattern Story 5.2 established on the Dashboard page).
  - [ ] Do **not** add the panel to Master Data (Story 5.2's read-only view already communicates "this is a mirror" via its own "Open at Site X" links — a second, redundant staleness indicator there isn't asked for by any AC and would clutter a page this story doesn't need to touch).
- [ ] Task 8: Testing — frontend (AC: #1, #2, #3)
  - [ ] New file `web/oee-shell/src/app/shared/sync-status/sync-status-panel.spec.ts`: renders one badge per returned site; a stale site gets the `sync-badge--stale` class and a never-synced site (`lastSyncedAt: null`) renders the `neverSynced` label, not a nonsensical "NaN minutes ago"; a fresh site renders the plain (non-stale) style.
  - [ ] Extend `dashboard-page.spec.ts` (Story 5.2) and `reports-page.spec.ts` (Story 4.1): the panel renders when `AppModeService.mode()` is `'Central'` and is absent when `'Site'`.

## Dev Notes

- **This story is a thin read/display layer on top of Story 5.1's write path — don't re-derive the sync-tracking mechanism.** `SiteSyncStatus` and its upsert-on-ingest already exist; Task 1 only adds a list query, and Tasks 3-4 only shape/expose it. If this story's implementation starts touching `ReceiveSyncBatchUseCase` or `SyncIngestRepository`, stop — that's Story 5.1's territory and shouldn't need to change here.
- **"Never synced" (no `SiteSyncStatus` row) is not an edge case to skip — it's arguably the most important state this story surfaces**, since it's the case most likely to actually indicate a real problem (a site that's never successfully reached Central at all, vs. one that's merely a few minutes behind schedule). Building the query by enumerating `Site` first and left-joining status (Task 3), rather than enumerating `SiteSyncStatus` and joining site names, is what guarantees this case can't be silently dropped.
- **The distinct icon/color requirement (Task 6, AC #3) is not a cosmetic nice-to-have — it's the literal mechanism satisfying AC #3.** AC #3's "phân biệt được đây là vấn đề đồng bộ chứ không phải máy hỏng" only holds if the sync badge is visually unmistakable from `MachineStatusCard`'s no-signal state. Don't take a shortcut and reuse `pi-ban`/`--status-no-signal` for convenience.
- **Client-computed relative time, not a live ticking clock.** `MachineStatusCard`'s no-signal label recomputes every second via `ClockTickService` because a machine's connectivity is genuinely a real-time concern (Story 2.3). Cross-site sync state explicitly isn't (AD-8, this epic's AC #2) — computing "X minutes ago" once when the panel loads, without wiring up `ClockTickService`, is correct here, not a missed optimization.
- **`WarningThresholdMinutes` living in the same `SyncOptions` class as Story 5.1's `Enabled`/`CentralBaseUrl`/`ApiKey`/`IntervalSeconds` is deliberate reuse, not scope bleed.** One shared `Sync` config section, different fields relevant depending on which `AppMode` reads it — the same reasoning Story 5.1 already established for `ApiKey` (Site sends it, Central validates it) applies here for the new field (Central reads it, Site ignores it).

### Project Structure Notes

- Modified: `src/OeeNew.Application/Sync/ISyncStatusRepository.cs` (new `ListAllAsync` + `SiteSyncStatusRecord`), `src/OeeNew.Infrastructure/Persistence/SyncStatusRepository.cs` (implementation), `src/OeeNew.Infrastructure/Sync/SyncOptions.cs` (new `WarningThresholdMinutes` field), `src/OeeNew.Api/appsettings.json` (`Sync:WarningThresholdMinutes`).
- New file: `src/OeeNew.Application/Sync/SyncStatusQueryUseCase.cs`.
- New file: `src/OeeNew.Api/Controllers/SyncStatusController.cs` (separate from Story 5.1's `SyncController` — see Task 4).
- New frontend files: `web/oee-shell/src/app/shared/sync-status/sync-status.service.ts`, `sync-status-panel.ts` (+ spec). Modified: `dashboard-page.ts` (Story 5.2's Central branch, extended), `reports-page.ts` (new Central branch), `en.json`/`vi.json`.
- No DB schema changes — `SiteSyncStatus` already exists (Story 5.1); this story only reads it.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-5] — Story 5.3 full AC (UX-DR11)
- [Source: _bmad-output/implementation-artifacts/5-1-site-to-central-sync.md] — `SiteSyncStatus` table, `ISyncStatusRepository.RecordSyncedAsync` write path, `SyncOptions` this story extends rather than duplicates
- [Source: _bmad-output/implementation-artifacts/5-2-central-cross-site-dashboard.md] — `AppModeService`/`isCentral()` frontend pattern this story's Dashboard/Reports wiring reuses; the Central-only Dashboard branch this story's panel slots into
- [Source: web/oee-shell/src/app/pages/dashboard/machine-status-card.ts] — `NO_SIGNAL_ICON`/`--status-no-signal` this story's badge must visually differ from (AC #3); the "color is never the only signal" accessibility convention this story's badge also follows (icon + text label together)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/DESIGN.md] — existing color tokens (`status-running/stopped/idle/no-signal`); `status-idle`'s amber (`#F59E0B`) reused as the value (not the token name) for this story's new `--sync-badge-stale`
- [Source: src/OeeNew.Application/MasterData/SiteManagementUseCase.cs] — the "list all, filter by `CallerScope`" shape `SyncStatusQueryUseCase.GetStatusesAsync` follows exactly
- [Source: src/OeeNew.Api/Program.cs] — existing `ReportsAccess` policy (Admin/Manager/Viewer) reused for `SyncStatusController`, instead of defining a new policy for one more endpoint
- [Source: web/oee-shell/src/app/pages/dashboard/loss-pie-chart.ts, oee-report.service.ts] — self-contained-widget/service shape `SyncStatusPanel`/`SyncStatusService` follow

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
