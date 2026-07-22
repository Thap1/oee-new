---
baseline_commit: f1932b2a23563d7141903e5b8b55da9b8f9ca004
---

# Story 5.2: Dashboard/Báo cáo tổng hợp xuyên site tại trung tâm

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin/Manager at the Central instance,
I want to view aggregated dashboards/reports spanning all my authorized sites,
so that I can see the whole operation without visiting each site separately.

## Acceptance Criteria

1. **Given** tôi đăng nhập vào instance Central (`AppMode=Central`) với quyền trên nhiều site **When** tôi mở Dashboard/Reports **Then** dữ liệu tổng hợp từ tất cả site tôi được phân quyền hiển thị cùng lúc (NFR-2)
2. **Given** dữ liệu tại trung tâm chỉ cập nhật theo chu kỳ Sync **When** tôi xem **Then** không có kỳ vọng cập nhật real-time cho dữ liệu xuyên site (trung tâm không cần real-time, AD-8)
3. **Given** tôi ở Central instance **When** tôi mở Master Data **Then** dữ liệu hiển thị read-only, kèm link "Mở tại site X" để thao tác ghi trực tiếp tại site đó (UX-DR5, AD-4)

## Tasks / Subtasks

- [ ] Task 1: Confirm and document the "almost free" backend consequence of Story 5.1's design (AC: #1)
  - [ ] **No new backend query code for Reports (4.1-4.3) or the Loss Pie Chart (3.1/3.2).** Both are built entirely on `CallerScope`-filtered repository calls (`IMachineRepository.ListByScopeAsync`, `IDowntimeEventRepository.ListClosedSlicesInRangeAsync`, etc.) against `OeeDbContext` — they have no idea whether they're running against a Site's or Central's Postgres instance. Story 5.1 lands synced `Site`/`Line`/`Machine`/`ReasonCode`/closed-`DowntimeEvent`/`QualityReject` rows into Central's own local DB using the exact same tables. Once those rows exist, `GET /api/reports/oee` and the Loss Pie Chart's endpoints already return correct cross-site aggregates for any Admin (global scope) or multi-site Manager/Viewer (their JWT's `siteId`/`lineIds` claims, unchanged since Story 1.6) — **do not add a parallel "central reporting" API.** This task is verification + a manual smoke test, not new production code.
  - [ ] Verify via a real-Postgres integration test (Task 6) that after seeding synced rows for two different `SiteId`s directly into one `OeeDbContext`, a global-scope `GET /api/reports/oee` call returns a report whose Availability/Performance/Quality losses sum across both sites' `DowntimeEvent`s — proving the existing aggregation code is already site-agnostic.
- [ ] Task 2: Backend — relocate `AppModeInfo` from `Program.cs` into `Application`, so use cases can depend on it (AC: #3)
  - [ ] **This is a required correction, not optional cleanup.** `AppModeInfo` currently lives as a top-level-statement type in `src/OeeNew.Api/Program.cs:243` (`public sealed record AppModeInfo(string Mode);`), used so far only by Story 5.1's `SyncController` (an `Api`-layer class — fine, since `Api` may depend on anything). Task 4 of *this* story needs the same mode check **inside** `MasterData` use cases (`Application` layer) — and `Application` must never depend on a type declared in `Api` (AD-1: Api → Application → Domain, never the reverse). Moving the type fixes this without changing its registration or behavior.
  - [ ] New file `src/OeeNew.Application/AppModeInfo.cs`:
    ```csharp
    namespace OeeNew.Application;

    /// <summary>AppMode: Site | Central (Architecture Spine AD-2), resolved once at startup and injected as a singleton. Lives in Application (not Api) so use cases can depend on it directly (AD-1) — see Story 5.2 Task 2.</summary>
    public sealed record AppModeInfo(string Mode)
    {
        public bool IsCentral => Mode == "Central";
    }
    ```
  - [ ] `src/OeeNew.Api/Program.cs`: delete the local `public sealed record AppModeInfo(string Mode);` declaration (near the bottom of the file, after `app.Run()`), add `using OeeNew.Application;` at the top. `builder.Services.AddSingleton(new AppModeInfo(appMode));` (line ~41) is otherwise unchanged — same registration, just resolving the relocated type.
  - [ ] `src/OeeNew.Api/Controllers/SyncController.cs` (Story 5.1): add `using OeeNew.Application;` and replace its `appMode.Mode != "Central"` check with `!appMode.IsCentral` (the new convenience property) — purely a namespace/readability fix, no behavior change.
- [ ] Task 3: Backend — new `CentralReadOnlyException` (distinct from the existing role-based `MasterDataForbiddenException`) (AC: #3)
  - [ ] **Don't reuse `MasterDataForbiddenException` for this.** Its hardcoded message is `"Admin role is required for this operation."` (`src/OeeNew.Application/MasterData/MasterDataForbiddenException.cs`) — actively misleading here, since the caller blocked by this story's new check typically *is* a valid Admin; they're just pointed at the wrong (Central, read-only-mirror) instance. A distinct exception gives the FE a distinct error code to render a helpful, accurate message instead of a generic permission-denied one.
  - [ ] New file `src/OeeNew.Application/MasterData/CentralReadOnlyException.cs`:
    ```csharp
    namespace OeeNew.Application.MasterData;

    /// <summary>Thrown when a write is attempted against Site/Line/Machine/ShiftSchedule/ReasonCode master data at a Central (AppMode=Central) instance — AD-4: master data is owned and writable only at its origin site, never proxied through Central even for a global-scope Admin JWT (Story 5.2, UX-DR5).</summary>
    public sealed class CentralReadOnlyException() : Exception("This data is read-only at the Central instance. Make this change at the owning Site instance instead.");
    ```
  - [ ] `src/OeeNew.Api/Errors/ApiExceptionHandler.cs`: add one more arm to `Map`: `CentralReadOnlyException centralReadOnly => (StatusCodes.Status403Forbidden, new ApiErrorResponse { Code = "CENTRAL_READ_ONLY", Message = centralReadOnly.Message }),` — same envelope shape as every other mapped exception, no new response type.
  - [ ] `src/OeeNew.Application/MasterData/MasterDataAuthorization.cs`: add a companion guard next to the existing `EnsureAdmin`:
    ```csharp
    public static void EnsureNotCentral(AppModeInfo appMode)
    {
        if (appMode.IsCentral)
        {
            throw new CentralReadOnlyException();
        }
    }
    ```
- [ ] Task 4: Backend — gate every Site/Line/Machine/ShiftSchedule/ReasonCode **write** with the new guard; leave User management and every read (`List*Async`) untouched (AC: #3)
  - [ ] **Scope this precisely to AD-4's own list — "Site, Line, Machine, ShiftSchedule, ReasonCode, và phần gán vai trò + phạm vi site/line của User" — but `User` credential *creation* is explicitly the Central Identity Provider's job per AD-7, a different rule for a different reason.** This codebase's `CentralCredentialProvisioner` (`src/OeeNew.Infrastructure/Identity/CentralCredentialProvisioner.cs`) is already documented as a same-process stub standing in for "the real central identity authority" until true multi-instance deployment exists — blocking `UserManagementUseCase` here would contradict AD-7's design (Admin needs *some* place to create users, and AD-7 says that place is conceptually Central) and is out of this story's scope. Do not touch `UsersController`/`UserManagementUseCase`.
  - [ ] Add `AppModeInfo appMode` as a constructor parameter to all five MasterData use cases, and call `MasterDataAuthorization.EnsureNotCentral(appMode)` as the **first** line of every write method (before the existing `EnsureAdmin` check, or after — order doesn't matter functionally, but put it first so a Central-mode Admin gets the accurate `CENTRAL_READ_ONLY` message rather than incidentally passing the Admin check and hitting some other guard first):
    - `src/OeeNew.Application/MasterData/SiteManagementUseCase.cs`: `CreateAsync`, `RenameAsync`, `DeleteAsync`. Constructor becomes `(ISiteRepository sites, ILineRepository lines, AppModeInfo appMode)`.
    - `src/OeeNew.Application/MasterData/LineManagementUseCase.cs`: `CreateAsync`, `RenameAsync`, `DeleteAsync`.
    - `src/OeeNew.Application/MasterData/MachineManagementUseCase.cs`: `CreateAsync`, `RenameAsync`, `DeleteAsync`.
    - `src/OeeNew.Application/MasterData/ShiftScheduleManagementUseCase.cs`: `CreateAsync`, `RescheduleAsync`, `DeleteAsync`.
    - `src/OeeNew.Application/MasterData/ReasonCodeManagementUseCase.cs`: `CreateAsync`, `DeactivateAsync`, `DeleteAsync`.
  - [ ] **This is the same kind of ripple Story 3.2/4.3 caused adding a dependency to an existing, already-tested class** — every direct construction of these five use cases in `tests/OeeNew.Application.Tests/MasterData/*Tests.cs` needs the new `appMode` argument (use `new AppModeInfo("Site")` for every existing/positive-path test, since they all implicitly assume Site-mode today), plus new tests asserting `new AppModeInfo("Central")` → `CentralReadOnlyException` from every write method. `Program.cs` needs no explicit change for these five registrations — `AppModeInfo` is already a registered singleton (Task 2), the DI container resolves the new constructor parameter automatically.
- [ ] Task 5: Backend — expose `AppMode` to the frontend, and a Central-only "open at site" link on `Site` (AC: #1, #3)
  - [ ] New file `src/OeeNew.Api/Controllers/AppModeController.cs`:
    ```csharp
    [ApiController]
    [Route("api/app-mode")]
    [AllowAnonymous]
    public sealed class AppModeController(AppModeInfo appMode) : ControllerBase
    {
        [HttpGet]
        public ActionResult<object> Get() => Ok(new { mode = appMode.Mode });
    }
    ```
    `[AllowAnonymous]` deliberately — this needs to be readable by the login page / app shell before a JWT exists, same reasoning as the existing `/.well-known/jwks.json` anonymous endpoint (`Program.cs:233`). It carries no sensitive data (just "Site" or "Central").
  - [ ] New file `src/OeeNew.Infrastructure/MasterData/CentralOptions.cs`:
    ```csharp
    public sealed class CentralOptions
    {
        public const string SectionName = "Central";
        /// <summary>Site.Id (string GUID) -> that site's own base URL, so Central's read-only Master Data view can link out to "Mở tại site X" (UX-DR5). Populated by ops when a site is onboarded — not synced data, deliberately a static config map instead of a new Site.BaseUrl column: this is a handful of on-prem URLs, not something that changes often enough to justify extending Story 5.1's synced Site schema/wire payload for it.</summary>
        public Dictionary<string, string> SiteLinks { get; set; } = [];
    }
    ```
    Registered in `Program.cs`: `builder.Services.Configure<CentralOptions>(builder.Configuration.GetSection(CentralOptions.SectionName));` — empty by default, ops fills in `appsettings.json`/environment config per deployment, e.g. `"Central": { "SiteLinks": { "<site-guid>": "https://site-a.oee.local" } } }`.
  - [ ] `src/OeeNew.Api/Controllers/SitesController.cs`: extend `SiteResponse` with an optional field: `public sealed record SiteResponse(Guid Id, string Name, string? OpenAtUrl);`. Inject `IOptions<CentralOptions>` and `AppModeInfo` into the controller; in `ToResponse`, set `OpenAtUrl = appMode.IsCentral ? centralOptions.Value.SiteLinks.GetValueOrDefault(site.Id.ToString()) : null` (always `null` at a Site instance — the link only makes sense from Central).
- [ ] Task 6: Testing — backend (all AC)
  - [ ] New file `tests/OeeNew.Application.Tests/MasterData/CentralReadOnlyTests.cs` (or extend each existing `*ManagementUseCaseTests.cs` — prefer extending, matching how Story 3.2/4.3 folded their ripple into existing test files rather than creating a parallel file per class): for each of the five use cases, a `new AppModeInfo("Central")` construction + a call to each write method throws `CentralReadOnlyException`; a `new AppModeInfo("Site")` construction behaves exactly as before (regression check — reuse the existing positive-path assertions, just with the new constructor argument threaded through).
  - [ ] New file `tests/OeeNew.Api.Tests/MasterData/CentralReadOnlyEndpointsTests.cs`, real-Postgres full-flow style (matching existing `*EndpointsTests.cs`): an Admin token against a `Central`-mode test host gets `403 CENTRAL_READ_ONLY` from `POST /api/master-data/sites` (and one representative write endpoint each for Line/Machine/ShiftSchedule/ReasonCode — no need to re-test every combination exhaustively, one per resource is enough since Task 4's guard is structurally identical across all five); the same request against a `Site`-mode host still succeeds (regression).
  - [ ] Extend `tests/OeeNew.Api.Tests/Reports/ReportsEndpointsTests.cs` (or a new `tests/OeeNew.Api.Tests/Reports/CrossSiteReportTests.cs`) with Task 1's cross-site aggregation proof: seed closed `DowntimeEvent`s under two different `Site`/`Line`/`Machine` chains in the one test DB, assert a global-scope `GET /api/reports/oee` sums losses across both.
  - [ ] New file `tests/OeeNew.Api.Tests/AppModeEndpointTests.cs`: `GET /api/app-mode` with no `Authorization` header still succeeds (anonymous) and returns the test host's configured mode.
  - [ ] `SitesController`'s response mapping: extend the existing Sites endpoint test (or add one) confirming `openAtUrl` is `null` when `AppMode=Site` regardless of `Central:SiteLinks` config, and populated only for a `Site.Id` present in `Central:SiteLinks` when `AppMode=Central`.
- [ ] Task 7: Frontend — `AppModeService`, a small shared "which mode am I in" signal (AC: #1, #2, #3)
  - [ ] New file `web/oee-shell/src/app/core/app-mode/app-mode.service.ts`, `@Injectable({ providedIn: 'root' })`: a `private readonly modeSignal = signal<'Site' | 'Central' | null>(null)` plus `readonly mode = this.modeSignal.asReadonly()` and `async load(): Promise<void> { if (this.modeSignal() !== null) return; const res = await firstValueFrom(this.http.get<{ mode: 'Site' | 'Central' }>('/api/app-mode')); this.modeSignal.set(res.mode); }` — cached after first successful call (same "call once, cache in a signal" shape as `AuthService`'s role/token state), so both `DashboardPage` and `MasterDataPage` share one fetch instead of duplicating it. `readonly isCentral = computed(() => this.mode() === 'Central');`
  - [ ] Call `appMode.load()` once during bootstrap (e.g. `Shell`'s `ngOnInit`, `web/oee-shell/src/app/core/layout/shell.ts` — confirm the exact file first; the goal is "resolved before `DashboardPage`/`MasterDataPage` render," not a specific file) so `mode()` is already populated by the time either page's `ngOnInit` reads it — avoid a loading flicker where the Machine Status Card grid briefly renders before being hidden.
- [ ] Task 8: Frontend — suppress the live Machine Status Card grid at Central; keep the Loss Pie Chart (AC: #1, #2)
  - [ ] **Why this is necessary, not cosmetic:** at Central, `MachineState` rows never exist for any machine — Story 5.1's sync payload deliberately excludes raw/live signal state (AD-2: only closed business records sync). Every synced `Machine` would show through `MachineStatusQueryUseCase` (Story 2.2) as its default never-configured/no-signal skeleton state — permanently, for every machine, at every site. Left as-is, a Central Admin would see a wall of false "no signal" cards implying every machine everywhere is disconnected, which is simply wrong. AC #2's "no real-time expectation at Central" is the explicit signal that the live-card view doesn't belong there at all.
  - [ ] `web/oee-shell/src/app/pages/dashboard/dashboard-page.ts`: inject `AppModeService`; when `appMode.isCentral()` is `true`, skip `loadMachineStates()`/`hub.connect()` entirely in `ngOnInit` and don't render the `dashboard-grid`/empty-state block — render only the `<app-loss-pie-chart>` (already the last element in the template) under a heading that reads e.g. `dashboard.centralAggregateTitle` instead of `nav.dashboard`, so it's clear this is the cross-site aggregate view, not a live shopfloor view. Reason Code Picker (`onCardTapped`) is unreachable in this mode by construction (no cards render), needs no separate guard.
  - [ ] i18n: add `dashboard.centralAggregateTitle` / a short explanatory subtitle key to `en.json`/`vi.json`.
- [ ] Task 9: Frontend — Master Data read-only at Central, with "Open at Site X" links (AC: #3)
  - [ ] `web/oee-shell/src/app/pages/master-data/master-data.service.ts`: extend the `SiteDto` interface with `openAtUrl: string | null` (matches Task 5's `SiteResponse.OpenAtUrl`, camelCased per the existing DTO convention).
  - [ ] `web/oee-shell/src/app/pages/master-data/master-data-page.ts`: inject `AppModeService`; add `readonly isReadOnly = computed(() => this.appMode.isCentral());`. Guard every Site/Line/Machine/Shift/ReasonCode create/edit/delete/deactivate trigger (`openCreate`, `openEdit`, `deleteSite`, `deleteLine`, `deleteMachine`, `openCreateShift`, `openEditShift`, `deleteShift`, `openCreateReasonCode`, `deactivateReasonCode`, `deleteReasonCode`) with an early return when `isReadOnly()` is `true` — belt-and-suspenders alongside hiding the triggering buttons in the template (`*ngIf="!isReadOnly()"` / `@if (!isReadOnly())` on each create/edit/delete button), matching this codebase's established "hidden UI is UX polish, the real boundary is the backend check (Task 4)" pattern already used for Operator route-guarding (Story 4.1 Task 7) and master-data's own existing sidebar-only gating (`deferred-work.md`). **Do not guard `openCreateUser`/user actions** — Users are out of this story's read-only scope (Task 4).
  - [ ] `master-data-page.html`: for each Site row, when `isReadOnly()` and `site.openAtUrl` is non-null, render an `<a [href]="site.openAtUrl" target="_blank" rel="noopener">{{ 'masterData.openAtSite' | translate }}</a>` link (UX-DR5's "Mở tại site X"); when `isReadOnly()` and `site.openAtUrl` is `null` (no `Central:SiteLinks` entry configured for that site), show nothing extra — don't render a dead/placeholder link.
  - [ ] i18n: add `masterData.openAtSite` and a `masterData.error.centralReadOnly` key (for the `CENTRAL_READ_ONLY` error code) to `en.json`/`vi.json`; extend `describeError()` in `master-data-page.ts` with `if (body?.code === 'CENTRAL_READ_ONLY') { return this.translate.instant('masterData.error.centralReadOnly'); }` alongside the existing `FORBIDDEN`/`HAS_DEPENDENTS`/etc. branches — this should be unreachable in practice once the buttons are hidden (previous bullet), but the backend check (Task 4) is the real boundary and needs a real message for the rare direct-API-call case.
- [ ] Task 10: Testing — frontend (AC: #1, #2, #3)
  - [ ] New file `web/oee-shell/src/app/core/app-mode/app-mode.service.spec.ts`: `load()` fetches once and caches (a second `load()` call doesn't re-issue the HTTP request); `isCentral()` reflects the fetched mode.
  - [ ] Extend `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts`: when `AppModeService.mode()` is `'Central'`, the component does not call `listMachineStates()`/`hub.connect()` and does not render `dashboard-grid`; the Loss Pie Chart still renders.
  - [ ] Extend `web/oee-shell/src/app/pages/master-data/master-data-page.spec.ts`: when `AppModeService.mode()` is `'Central'`, create/edit/delete buttons for Site/Line/Machine/Shift/ReasonCode are absent from the rendered template; the "Open at Site X" link renders when `openAtUrl` is present, and is absent when it's `null`; User-management controls remain visible/functional regardless of mode.

## Dev Notes

- **This story's central insight: Story 5.1 already did almost all of the hard work.** Because `AppMode=Site` and `AppMode=Central` run the identical binary against the identical `OeeDbContext` schema, every `CallerScope`-filtered query written for Epic 2/3/4 is already cross-site-correct the moment synced rows land in Central's DB. Resist the temptation to build a "Central reporting service" or duplicate query logic — if you find yourself writing a new aggregation query for this story, stop and check whether an existing repository/use-case method already does it once data exists.
- **`AppModeInfo`'s relocation (Task 2) is a real architectural fix, not incidental refactoring — call it out in the PR/completion notes as such.** It was fine for `AppModeInfo` to live in `Program.cs` back when Story 5.1's `SyncController` was its only consumer (an `Api`-layer class). This story is the first to need it from inside `Application` (the MasterData use cases), which is the point at which "just defined in Program.cs" stops being AD-1-compliant.
- **Two different "Forbidden" reasons now exist for master-data writes — keep them distinct.** `MasterDataForbiddenException` (existing) = "you are not an Admin." `CentralReadOnlyException` (new, this story) = "you are an Admin, but this specific instance never accepts this write." Collapsing them back into one generic exception would regress the accurate error messaging this story adds.
- **Users are explicitly out of scope for the read-only lock, and that's an AD-7 vs AD-4 distinction, not an oversight.** Don't "complete the picture" by also locking `UsersController` — AD-7 assigns user-credential provisioning to the (conceptual) central identity authority, which is a different ownership rule than AD-4's site-owned Site/Line/Machine/ShiftSchedule/ReasonCode list. `CentralCredentialProvisioner`'s doc comment already flags this whole area as an accepted MVP simplification pending real multi-instance identity — not this story's problem to solve.
- **The Machine Status Card suppression (Task 8) is the one place this story changes existing Epic 2 UI behavior — scope it narrowly.** Only the live-card grid is hidden at Central; the Loss Pie Chart, Reports, and Master Data (now read-only) all continue to render normally. Don't disable SignalR globally or touch `MachineStatusHubService` itself — a Site instance still needs it exactly as before.
- **`Central:SiteLinks` is a deliberately simple, ops-managed config map, not a new `Site.BaseUrl` column.** Adding a synced URL field would mean extending `Site` (Domain), `SyncSiteRecord` (Story 5.1's wire contract), the sync ingest upsert logic, and a new migration — a lot of ripple for a handful of on-prem URLs that change only when a site is onboarded/decommissioned, which is already an ops/deployment-time event (new appsettings + redeploy) in this architecture. If self-service URL editing becomes a real requirement later, promote it to a synced field then.

### Project Structure Notes

- New file `src/OeeNew.Application/AppModeInfo.cs`; **deleted** local declaration in `src/OeeNew.Api/Program.cs`.
- New file `src/OeeNew.Application/MasterData/CentralReadOnlyException.cs`; extended `MasterDataAuthorization.cs` with `EnsureNotCentral`.
- Modified: `SiteManagementUseCase.cs`, `LineManagementUseCase.cs`, `MachineManagementUseCase.cs`, `ShiftScheduleManagementUseCase.cs`, `ReasonCodeManagementUseCase.cs` (constructor + guard in every write method); `ApiExceptionHandler.cs` (new exception mapping); `SyncController.cs` (Story 5.1 — namespace/property fix only).
- New files: `src/OeeNew.Api/Controllers/AppModeController.cs`, `src/OeeNew.Infrastructure/MasterData/CentralOptions.cs`. Modified: `SitesController.cs` (`SiteResponse.OpenAtUrl`), `Program.cs` (`Configure<CentralOptions>`).
- New frontend files: `web/oee-shell/src/app/core/app-mode/app-mode.service.ts` (+ spec). Modified: `dashboard-page.ts` (+ template), `master-data-page.ts` + `.html`, `master-data.service.ts` (`SiteDto.openAtUrl`), `en.json`/`vi.json`.
- No DB schema changes — this story is entirely application-layer guards + read-model shaping + frontend conditional rendering over data Story 5.1 already lands.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-5] — Story 5.2 full AC (NFR-2, AD-8, UX-DR5, AD-4)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md] — AD-1 (dependency direction, why `AppModeInfo` must move), AD-4 (master-data ownership/read-only-at-Central rule this story enforces), AD-7 (why Users are excluded), AD-8 (no real-time expectation at Central), UX-DR5
- [Source: _bmad-output/implementation-artifacts/5-1-site-to-central-sync.md] — the sync mechanism whose output this story reads; `AppModeInfo`'s prior (Api-layer) location and `SyncController`'s existing mode-guard pattern, which Task 2/3 generalize
- [Source: src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs, src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs] — confirms both are pure `CallerScope`-filtered aggregations with no site-locality assumption, the basis for Task 1's "no new backend query code" conclusion
- [Source: src/OeeNew.Application/MasterData/MasterDataAuthorization.cs, MasterDataForbiddenException.cs] — existing Admin-role guard pattern this story's `EnsureNotCentral`/`CentralReadOnlyException` directly mirrors
- [Source: src/OeeNew.Api/Program.cs] — existing `AppModeInfo` singleton registration, `AllowAnonymous` anonymous-endpoint precedent (`/.well-known/jwks.json`) reused for `AppModeController`
- [Source: src/OeeNew.Infrastructure/Identity/CentralCredentialProvisioner.cs] — doc comment confirming the same-process-stub status of central identity provisioning, the reasoning behind excluding Users from this story's read-only scope
- [Source: web/oee-shell/src/app/pages/dashboard/dashboard-page.ts] — existing Machine Status Card + Loss Pie Chart composition this story conditionally narrows at Central
- [Source: web/oee-shell/src/app/pages/master-data/master-data-page.ts, master-data.service.ts] — existing single-page CRUD surface (Site/Line/Machine/Shift/ReasonCode/User tabs) this story adds read-only gating to, excluding the User tab
- [Source: _bmad-output/implementation-artifacts/4-1-oee-report-shift-day-week.md] — precedent for a narrowly-scoped client-side route/UI guard backed by a server-side check as the real boundary (Task 7 there, mirrored by Task 9 here)
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — the previously-accepted "sidebar-only" gating gap for `/master-data`, distinguished from this story's explicit read-only requirement (AC #3)

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
