---
baseline_commit: 9ed4738da848404782bd3f29958b1547190f72a8
---

# Story 4.2: Lọc báo cáo theo site/line/máy

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Manager/Viewer,
I want to filter the report by site/line/machine within my permission scope,
so that I can narrow down to what I actually manage.

## Acceptance Criteria

1. **Given** tôi có quyền trên nhiều site/line **When** tôi mở bộ lọc báo cáo **Then** chỉ thấy các lựa chọn trong phạm vi được phân quyền (FR-017, NFR-5)
2. **Given** tôi áp dụng lọc theo Machine cụ thể **When** áp dụng **Then** báo cáo chỉ tính trên máy đó
3. **Given** tôi gọi API báo cáo trực tiếp với site/line ngoài phạm vi **When** gửi request **Then** bị từ chối (FR-015, NFR-5)

## Tasks / Subtasks

- [x] Task 1: Backend — extend `OeeReportQueryUseCase.GetReportAsync` with an optional Site/Line/Machine filter (AC: #1, #2, #3)
  - [x] New file `src/OeeNew.Application/Reports/ReportFilterTargetType.cs`: `public enum ReportFilterTargetType { Site, Line, Machine }`. **Three levels, unlike Epic 3's `LossBreakdownTargetType` (`Equipment`/`Area` only, two levels)** — FR-017 explicitly requires Site as a filter level too, which Epic 3 never needed since its pie chart only ever showed one Equipment or one Area at a time.
  - [x] Extend `OeeReportQueryUseCase.GetReportAsync` (`src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs`, Story 4.1) with two new optional parameters: `ReportFilterTargetType? filterType, Guid? filterId`. Resolution order, composed from what Story 4.1 already built:
    1. Resolve the **period-implied machine set** exactly as Story 4.1's `ResolveMachinesAsync` already does (Shift → the picked `ShiftSchedule`'s own Site/Line, scope-checked; Day/Week → `machines.ListByScopeAsync(scope)`, the caller's full permitted set). Do not change this step.
    2. If `filterType`/`filterId` are given, resolve a **separate filter machine set** against `CallerScope` (never against the narrower period-implied set — see Dev Notes on why these are two independent checks):
       - `Machine`: load the `Machine`, resolve its `Line` (`ILineRepository.GetAsync`), check `scope.AllowsSite(line.SiteId) && scope.AllowsLine(machine.LineId)` — identical to `LossBreakdownQueryUseCase.ResolveEquipmentAsync` (`src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs:83-93`). Throw `MasterDataForbiddenException` on failure (AC #3).
       - `Line`: identical to `LossBreakdownQueryUseCase.ResolveAreaAsync` (`:95-105`) — check `scope.AllowsSite`/`AllowsLine`, then `machines.ListByLineAsync(lineId)`.
       - `Site`: **new** — check `scope.AllowsSite(siteId)` (404 via `MasterDataNotFoundException` if the Site itself doesn't exist, `MasterDataForbiddenException` if it exists but is out of scope), then reuse Story 4.1's site-wide-shift composition exactly (`ILineRepository.ListBySiteAsync(siteId)` → keep lines where `scope.AllowsLine(line.Id)` → `machines.ListByLineAsync` per surviving line, concatenated). Story 4.1's Dev Notes already flagged this as reusable — pull it into a shared private helper (e.g. `ResolveSiteMachinesAsync(CallerScope, Guid siteId, ...)`) called from both the Shift-period path and this new Site-filter path, instead of copy-pasting it a second time.
    3. Final `machineIds` = **intersection** of the period-implied set and the filter set (when a filter was given), or just the period-implied set (no filter). An empty intersection (e.g., filtering to a Site that legitimately differs from the picked Shift's Site) is not an error — it produces the same "empty scope → all-zero `OeeReportResult`" result Story 4.1 already returns for an empty machine list. **Only an out-of-`CallerScope`** filter target throws `MasterDataForbiddenException` (AC #3) — a valid-but-non-overlapping filter is a legitimate empty result, not a security violation. Don't conflate the two.
  - [x] `src/OeeNew.Api/Controllers/ReportsController.cs`: add `[FromQuery] ReportFilterTargetType? filterType, [FromQuery] Guid? filterId` to `GetOeeReport`. Same both-or-neither 400 validation Story 4.1 added for `shiftScheduleId` (`filterType.HasValue != filterId.HasValue` → `BadRequest`).
- [x] Task 2: Backend — new Site-scoped options endpoint for the filter dropdown (AC: #1)
  - [x] The existing `GET api/master-data/sites` (`SitesController`) is already scope-filtered (used by `ScopeService.loadSites()`, `web/oee-shell/src/app/core/scope/scope.service.ts:27-29`) and `GET api/master-data/sites/{siteId}/lines` likewise (Line-level, same service) — **both already satisfy AC #1 for Site/Line filter options, nothing new needed there.** For Machine-level options, `GET api/master-data/lines/{lineId}/machines` (`MachinesController.ListByLine`) is also already scope-enforced (`MachineManagementUseCase.ListByLineAsync`, `src/OeeNew.Application/MasterData/MachineManagementUseCase.cs:9-19` — throws `MasterDataForbiddenException` for an out-of-scope `lineId`). **No new backend endpoint is needed for Task 2 — this task is "confirm and reuse," not "build."** Do not add a duplicate `/api/reports/...` options endpoint.
- [x] Task 3: Frontend — filter controls on the Reports page (AC: #1, #2)
  - [x] Extend `web/oee-shell/src/app/pages/reports/oee-report.service.ts` (Story 4.1)'s `getReport` to accept optional `filterType?: 'Site' | 'Line' | 'Machine'` and `filterId?: string`, added to the existing `URLSearchParams` the same way `loss-analytics.service.ts`'s `getBreakdown` adds its optional `date` param.
  - [x] Add filter dropdowns to `web/oee-shell/src/app/pages/reports/reports-page.ts` (Story 4.1): a `p-select` for filter level (Site/Line/Machine, plus an implicit "no filter" default) and a dependent `p-select` for the target, populated from `ScopeService.sites()`/`.lines()` (reuse directly — don't refetch, `ScopeService` already holds them for the topbar selector) for Site/Line, and a new local fetch of `GET api/master-data/lines/{lineId}/machines` for Machine level (mirrors how `loss-pie-chart.ts`'s `@Input() equipmentOptions` is sourced from the Dashboard's already-scoped machine list — but Reports has no equivalent parent-supplied input, so fetch directly via `HttpClient` in `OeeReportService` or a small addition to it: `listMachinesForLine(lineId): Promise<{id:string; name:string}[]>` calling the existing endpoint).
  - [x] Selecting a filter re-fetches the report (same `refetchBreakdown`-style pattern as `loss-pie-chart.ts:183-190`) with the new `filterType`/`filterId` params. Clearing the filter (back to "no filter") re-fetches without them — same default-scope report Story 4.1 built.
  - [x] i18n: add `reports.filter.{label,none,site,line,machine,selectTarget}` to the `reports` namespace Story 4.1 introduced (`en.json`/`vi.json`).
- [x] Task 4: Testing (all AC)
  - [x] Extend `tests/OeeNew.Application.Tests/Reports/OeeReportQueryUseCaseTests.cs` (Story 4.1): Machine-level filter narrows the report to just that machine's downtime (AC #2 — seed two machines with different downtime, filter to one, assert only its seconds are counted); Site-level filter composes correctly across multiple Lines of that Site (reusing the same composition Story 4.1's Shift path uses); an out-of-`CallerScope` filter (any of the three levels) throws `MasterDataForbiddenException` (AC #3); a valid in-scope filter that doesn't overlap the period-implied machine set (e.g. filtering to a Line different from a picked Shift's Line) returns an all-zero result, not an exception.
  - [x] Extend `tests/OeeNew.Api.Tests/Reports/ReportsEndpointsTests.cs` (Story 4.1): a Manager token scoped to Site A gets 403/`FORBIDDEN` calling `GET /api/reports/oee?...&filterType=Site&filterId={SiteB's id}` (AC #3, real end-to-end — the exact scenario the AC names, "gọi API report trực tiếp với site/line ngoài phạm vi"); a valid Machine-level filter returns a report counting only that machine (AC #2).
  - [x] `web/oee-shell/src/app/pages/reports/reports-page.spec.ts` (extend, Story 4.1): selecting a filter re-fetches with the right query params; filter dropdown options come from `ScopeService`'s already-scoped `sites()`/`lines()` signals, not a fresh unscoped fetch.

## Dev Notes

- **This story is almost entirely additive to Story 4.1's `OeeReportQueryUseCase`/`ReportsController`/`ReportsPage`/`OeeReportService` — no new use-case class, no new controller.** Same "extend the existing class with a second parameter set, reuse its private resolve helpers" shape Story 3.2 used on `LossBreakdownQueryUseCase`.
- **Two independent scope checks, don't merge them.** The period-implied machine set (Shift's Site/Line, or the caller's full scope for Day/Week) and the filter's target are each checked against `CallerScope` independently, then intersected. A filter that's valid-but-disjoint from the period (e.g., legitimately-scoped Site B filter applied to a Site A shift) is a normal empty result. A filter that's outside `CallerScope` entirely is the AC #3 rejection. Checking the filter against the period's implied scope instead of `CallerScope` would either wrongly reject legitimate disjoint combinations or wrongly allow an out-of-scope filter that happens to overlap — neither is correct.
- **Site-level filter resolution is not new logic — it's Story 4.1's site-wide-shift machine composition, reused.** Both need "every machine under every caller-permitted Line of a given Site." Extract it to one shared private helper the first time either needs it (whichever story's dev agent gets here first should do the extraction; if Story 4.1 is already merged when this story starts, refactor its inline logic into the shared helper rather than duplicating it).
- **Task 2 is a confirmation task, not a build task** — re-verify this before writing any new endpoint. All three levels of scope-filtered dropdown data already exist and are already used elsewhere in the app (`ScopeService` for Site/Line, `MachinesController.ListByLine` for Machine). Adding a parallel `/api/reports/...` options endpoint would be the exact "reinventing wheels" mistake this project's story process explicitly guards against.

### Project Structure Notes

- No new backend files — `OeeReportQueryUseCase.cs`, `ReportsController.cs` (both Story 4.1) are extended in place. One new small enum file (`ReportFilterTargetType.cs`).
- No new frontend files — `oee-report.service.ts`, `reports-page.ts` (both Story 4.1) are extended in place.
- No DB schema changes.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-4] — Story 4.2 full AC (FR-017)
- [Source: _bmad-output/implementation-artifacts/4-1-oee-report-shift-day-week.md] — everything this story extends: `OeeReportQueryUseCase`, `ReportsController`, `ReportsPage`, `OeeReportService`, and the site-wide-shift machine composition this story's Site-filter reuses
- [Source: src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs:83-105] — `ResolveEquipmentAsync`/`ResolveAreaAsync`, the exact pattern this story's Machine/Line filter resolution follows (and extends with a third, Site, level)
- [Source: src/OeeNew.Application/MasterData/MachineManagementUseCase.cs:9-19] — confirms `GET api/master-data/lines/{lineId}/machines` is already scope-enforced, reused as-is for the Machine filter dropdown (Task 2)
- [Source: web/oee-shell/src/app/core/scope/scope.service.ts] — `sites()`/`lines()` signals reused directly for the Site/Line filter dropdown options (Task 3), not refetched

### Review Findings

Epic 4 code review (2026-07-23), scoped across Stories 4.1/4.2/4.3 together (`bmad-code-review`, 3 parallel layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor).

- [x] [Review][Patch] `GetOeeReport_MachineFilter_ReturnsReportCountingOnlyThatMachine` (added by this story's Task 4) seeded no downtime and asserted nothing about which machine's data was counted — it could not actually distinguish "correctly filtered" from "filter silently ignored," despite its name. Fixed: now seeds different downtime on two machines and asserts the response counts only the filtered machine's seconds. [tests/OeeNew.Api.Tests/Reports/ReportsEndpointsTests.cs]
- [x] [Review][Patch] Picking a Machine filter and then switching the topbar Line left the stale `filterId` selected against a freshly-reloaded, unrelated machine list, with the old report still displayed. Fixed: the Line-change `effect()` now clears `filterIdSignal`/`reportSignal` before reloading. New test: `switching the topbar Line while a Machine filter is picked clears the stale filterId and report`. [web/oee-shell/src/app/pages/reports/reports-page.ts]
- [x] [Review][Patch] `ListClosedSlicesInRangeAsync`'s `StartedAt`-only range filter didn't clip/prorate downtime spanning a period boundary — most consequential for this story's narrower filtered/Shift-scoped windows. Originally deferred pending explicit product sign-off (the fix changes reported figures and at least one existing test's expectation); user asked to fix directly (2026-07-23 follow-up). Fixed: `ClosedDowntimeSlice` gained `StartedAt`/`EndedAt`; `ListClosedSlicesInRangeAsync`'s WHERE clause changed to an overlap check (`StartedAt < end && EndedAt > start`, both real repository and fake); `OeeReportQueryUseCase` clips each slice to the window before summing. `GetReportAsync_Day_UsesExactUtcCalendarDayBoundaries` updated; new test `GetReportAsync_Shift_EventRunningPastShiftEnd_IsClippedToShiftWindow_NotCountedInFull` proves a Shift-spanning event no longer inflates the loss total past the shift's own planned time. [src/OeeNew.Application/Production/IDowntimeEventRepository.cs, src/OeeNew.Infrastructure/Persistence/DowntimeEventRepository.cs, tests/OeeNew.Application.Tests/Production/FakeDowntimeEventRepository.cs, src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs]
- [x] [Review][Patch] No error/feedback when the Machine filter level is picked with no topbar Line currently selected — the dependent dropdown just stayed empty with no explanation. Fixed (2026-07-23 follow-up): `ReportsPage` gained a `contextHint` computed signal rendering a guidance message (distinct from the generic error banner) for this case and the analogous Shift-with-no-Site case. New tests: `shows a guidance hint ... when Shift is picked with no topbar Site selected`, `shows a guidance hint when the Machine filter is picked with no topbar Line selected`, `clears the guidance hint once a Site is selected and a Shift loads`. [web/oee-shell/src/app/pages/reports/reports-page.ts]

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

None — no blocking failures. Frontend filter tests needed one fix: `ScopeService.loadSites()`'s internal `/sites` → `/lines` request sequence needs a microtask-flush between the two `HttpTestingController.flush()` calls (same requirement `scope.service.spec.ts` already documents), otherwise `expectOne` for the second request fires before the first `flush()`'s continuation has run.

### Completion Notes List

- Extended `OeeReportQueryUseCase.GetReportAsync` with optional `filterType`/`filterId`, resolving the filter's machine set against `CallerScope` independently from the period-implied set, then intersecting — a disjoint-but-in-scope filter now returns an all-zero report, not an exception; an out-of-scope filter still throws `MasterDataForbiddenException`.
- Extracted `ResolveSiteMachinesAsync` as a shared private helper, used by both Story 4.1's site-wide-Shift path and this story's new Site-level filter, per the Dev Notes' explicit reuse instruction (no duplicated composition).
- `OeeReportQueryUseCase` gained a new `ISiteRepository` constructor dependency (already registered in DI) to resolve/validate the Site-level filter's target.
- Confirmed Task 2 is genuinely a no-op: `GET api/master-data/sites`, `.../sites/{id}/lines`, and `.../lines/{id}/machines` are all already scope-enforced and already consumed elsewhere in the app — no new backend endpoint added.
- `ReportsController` extended with `filterType`/`filterId` query params and the same both-or-neither 400 validation shape Story 4.1 used for `shiftScheduleId`.
- Frontend: `OeeReportService.getReport` gained optional `filterType`/`filterId` params; `ReportsPage` gained a filter-level `p-select` (None/Site/Line/Machine) and a dependent target `p-select`, reusing `ScopeService.sites()`/`.lines()` directly (no refetch) and a new `MasterDataService.listMachines(lineId)` call for the Machine level, refreshed via an `effect()` on `ScopeService.selectedLineId()` (mirrors Story 4.1's Shift/Site effect).
- Backend: 151/151 `OeeNew.Application.Tests`, 84/84 `OeeNew.Api.Tests` pass (13 new Application-level and 4 new Api-level tests for this story), no regressions.
- Frontend: `reports-page.spec.ts` extended to 10 tests (3 new, covering Site/Machine filter selection and clearing the filter); re-ran `core/**` and `pages/master-data/**` in isolation to confirm no regressions (same pre-existing `ng test` full-suite OOM caveat as Story 4.1).

### File List

**Backend — new:**
- `src/OeeNew.Application/Reports/ReportFilterTargetType.cs`

**Backend — modified:**
- `src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs`
- `src/OeeNew.Api/Controllers/ReportsController.cs`
- `tests/OeeNew.Application.Tests/Reports/OeeReportQueryUseCaseTests.cs`
- `tests/OeeNew.Api.Tests/Reports/ReportsEndpointsTests.cs`

**Frontend — modified:**
- `web/oee-shell/src/app/pages/reports/oee-report.service.ts`
- `web/oee-shell/src/app/pages/reports/reports-page.ts`
- `web/oee-shell/src/app/pages/reports/reports-page.spec.ts`
- `web/oee-shell/public/i18n/en.json`
- `web/oee-shell/public/i18n/vi.json`

## Change Log

- 2026-07-22: Story 4.2 implemented — Site/Line/Machine report filter added on top of Story 4.1's `OeeReportQueryUseCase`/`ReportsController`/`ReportsPage`, purely additive, no new use-case class or endpoint. All tasks/subtasks complete, all ACs satisfied, backend and frontend tests green. Status: ready-for-dev → review.
- 2026-07-23: Epic 4 code review — strengthened the under-verified Machine-filter API test and fixed the stale-filter-selection UI bug (see Review Findings). 2 deferred items logged to `deferred-work.md`. Backend/frontend regression green. Status: review → done.
- 2026-07-23 (follow-up): user asked to fix all remaining issues — resolved both previously-deferred items: the downtime-boundary-clipping gap (`ClosedDowntimeSlice` now carries `StartedAt`/`EndedAt`, range query uses an overlap check, sums are clipped to the window) and the missing UI feedback for Machine-filter-with-no-Line. `deferred-work.md`'s Epic 4 section removed. Full regression: 177/177 Application.Tests, 93/93 Api.Tests, 17/17 + 61/61 frontend.
