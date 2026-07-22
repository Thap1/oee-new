---
baseline_commit: 9ed4738da848404782bd3f29958b1547190f72a8
---

# Story 4.1: Xem báo cáo OEE tổng hợp theo ca/ngày/tuần

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Manager/Viewer,
I want to view aggregated OEE reports by shift/day/week,
so that I can review performance without manual Excel compilation.

## Acceptance Criteria

1. **Given** tôi là Manager/Viewer **When** tôi mở Reports **Then** tôi thấy báo cáo OEE (Availability×Performance×Quality) tổng hợp theo Ca, theo Ngày, và theo Tuần (FR-016)
2. **Given** tôi chọn kỳ báo cáo **When** chọn Ca cụ thể **Then** số liệu tính đúng theo Shift Schedule đã cấu hình ở Story 1.3
3. **Given** tôi là Operator **When** tôi cố mở Reports **Then** bị từ chối truy cập màn hình theo IA đã chốt (chỉ Manager/Viewer/Admin) (FR-015)

## Tasks / Subtasks

- [x] Task 1: Backend — OEE formula from scratch, using existing downtime-duration data (AC: #1, #2)
  - [x] **This is genuinely new math — nothing in the codebase computes an OEE ratio today.** Confirmed by reading every Domain entity: no `ProductionCount` table exists anywhere (it's only a named-but-unbuilt concept referenced by Epic 5/AD-2 for the sync module), `MachineState` holds only the *latest* cumulative counter (one row per machine, overwritten on every reading — see `src/OeeNew.Domain/Production/MachineState.cs`), and no `Machine`/`Line`/`Site` has an ideal-cycle-time or target-rate field. Epic 3's `LossBreakdownQueryUseCase` only sums downtime **seconds** per `LossCategory` — it never produces a percentage. `[ASSUMPTION]` (resolves FR-016's undefined formula, confirmed via user check-in this session): reuse exactly the same "seconds lost per LossCategory" data Epic 3 already established (`DowntimeEvent` duration grouped by its `ReasonCode.LossCategory`), rolled up as a **time-based-loss proxy**, not textbook count-based OEE:
    ```
    PlannedTimeSeconds = the period's total duration (see Task 2)
    Availability% = (PlannedTimeSeconds − AvailabilityLossSeconds) / PlannedTimeSeconds
    Performance%  = (PlannedTimeSeconds − AvailabilityLossSeconds − PerformanceLossSeconds) / (PlannedTimeSeconds − AvailabilityLossSeconds)
    Quality%      = (PlannedTimeSeconds − AvailabilityLossSeconds − PerformanceLossSeconds − QualityLossSeconds) / (PlannedTimeSeconds − AvailabilityLossSeconds − PerformanceLossSeconds)
    OEE% = Availability% × Performance% × Quality%
    ```
    If a stage's denominator is 0 (100% of the preceding time was already lost), define that stage's percentage as `0`, not `NaN`/divide-by-zero — write this as an explicit unit test. `QualityReject.Quantity` (Story 2.6) is surfaced as a **separate raw count** alongside `Quality%`, exactly like `LossBreakdownResult.QualityRejectQuantity` today — it is NOT blended into the ratio (there is no "total units produced" denominator to make it a true count-based Quality ratio; don't invent one).
  - [x] `DowntimeEvent`s with `LossCategory == null` (unattributed — machine resumed before Operator picked a reason, Story 2.5 Dev Notes: an accepted outcome) are excluded from all three sums, same as Epic 3's separate `UnattributedSeconds` bucket — do not fold them into Availability by default. Surface `UnattributedSeconds` in the new result record too, for the same transparency Epic 3 gives.
  - [x] New file `src/OeeNew.Application/Reports/OeeReportResult.cs`:
    ```csharp
    public sealed record OeeReportResult(
        ReportPeriodType PeriodType, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd,
        double AvailabilityPercent, double PerformancePercent, double QualityPercent, double OeePercent,
        long AvailabilityLossSeconds, long PerformanceLossSeconds, long QualityLossSeconds, long UnattributedSeconds,
        int QualityRejectQuantity);
    ```
- [x] Task 2: Backend — period resolution: Shift / Day / Week → a concrete `(DateTimeOffset Start, DateTimeOffset End)` window (AC: #1, #2)
  - [x] New file `src/OeeNew.Application/Reports/ReportPeriodType.cs`: `public enum ReportPeriodType { Shift, Day, Week }`
  - [x] **Day**: `[ASSUMPTION]` (extends the existing UTC-calendar-day convention `DowntimeEventRepository.ListClosedSlicesAsync`/`QualityRejectRepository.SumQuantityAsync` already use for their `DateOnly? date` filter, Story 3.2) — start = UTC midnight of `referenceDate`, end = `start.AddDays(1)`. `PlannedTimeSeconds = 86400` (full calendar day; there's no per-site "operating hours" concept anywhere in master data, so don't invent a shift-bounded planned time here — same reasoning Epic 3 used when it never excluded off-shift hours from a day's downtime sum).
  - [x] **Week**: `[ASSUMPTION]` — ISO-8601 week (Monday 00:00 UTC through the following Monday 00:00 UTC) containing `referenceDate`. Use `System.Globalization.ISOWeek` (`ISOWeek.GetYear`/`GetWeekOfYear`/`ToDateTime(year, week, DayOfWeek.Monday)` — built into .NET, no custom math needed) to find that Monday; `end = start.AddDays(7)`; `PlannedTimeSeconds = 7 * 86400`.
  - [x] **Shift**: requires an explicit `shiftScheduleId` (the "Ca cụ thể" of AC #2) plus `referenceDate` (which calendar date this shift instance falls on). Load the `ShiftSchedule` via `IShiftScheduleRepository.GetAsync` (`src/OeeNew.Application/MasterData/IShiftScheduleRepository.cs`) — 404 (`MasterDataNotFoundException`) if missing. Combine its `TimeOnly StartTime`/`EndTime` with `referenceDate` into an absolute UTC window, handling the overnight-wrap case (`EndTime < StartTime`, e.g. 22:00–06:00) the same way `ShiftSchedule.OverlapsWith`'s private `ToMinuteSegments()` already does for shift-overlap checks (`src/OeeNew.Domain/MasterData/ShiftSchedule.cs:73-87`) — don't duplicate that minute-segment logic, just apply the same wrap rule directly: `start = referenceDate + StartTime`; `end = EndTime > StartTime ? referenceDate + EndTime : referenceDate.AddDays(1) + EndTime`. `PlannedTimeSeconds = (end − start).TotalSeconds`.
  - [x] Reject `Shift` period type with no `shiftScheduleId` (and vice versa — `shiftScheduleId` given but `periodType != Shift`) at the API layer with a 400, before it ever reaches the use case.
- [x] Task 3: Backend — machine-scope resolution per period (AC: #1, #2, #3 — every path must stay inside `CallerScope`, NFR-5)
  - [x] **Day/Week** (no specific shift picked): resolve to every machine the caller can see — `IMachineRepository.ListByScopeAsync(scope, ct)` (`src/OeeNew.Application/Production` → actually defined in `src/OeeNew.Application/MasterData/IMachineRepository.cs`; "Every Machine across every Line/Site the caller is scoped to... Global scope returns everything" — Story 2.2). This is the same default-scope building block Story 4.2 will later narrow with explicit site/line/machine filters — don't hardcode an assumption here that blocks that extension.
  - [x] **Shift**: the picked `ShiftSchedule` already carries its own `SiteId`/optional `LineId` (Story 1.3) — this must be scope-checked, not trusted blindly (same reasoning as `LossBreakdownQueryUseCase.ResolveEquipmentAsync`/`ResolveAreaAsync`, `src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs:83-105`, which treat every client-supplied id as spoofable). Concretely:
    - `!scope.AllowsSite(shift.SiteId)` → throw `MasterDataForbiddenException` (AC #3-equivalent for a Manager/Viewer picking a shift outside their site).
    - If `shift.LineId` is set: `!scope.AllowsLine(shift.LineId.Value)` → forbidden; else machines = `machines.ListByLineAsync(shift.LineId.Value, ct)` (`IMachineRepository`).
    - If `shift.LineId` is null (site-wide shift): resolve every Line under `shift.SiteId` via `ILineRepository.ListBySiteAsync(shift.SiteId, ct)`, keep only lines where `scope.AllowsLine(line.Id)` (so a Line-restricted Operator-turned-Manager-scope... i.e. a Manager scoped to only some Lines of that Site never sees the other Lines' machines even though the shift is nominally site-wide), then `machines.ListByLineAsync` for each surviving line and concatenate. This composes entirely out of existing repository methods — no new repository method needed for this step.
  - [x] If the resolved machine list is empty (caller has zero machines in scope for this period), return an `OeeReportResult` with everything zeroed — same "empty scope → zeroed result, not an exception" shape `LossBreakdownQueryUseCase.GetAsync` already uses for an empty target.
- [x] Task 4: Backend — extend the two repositories with a **range** query (AC: #1, #2)
  - [x] `src/OeeNew.Application/Production/IDowntimeEventRepository.cs`: add
    ```csharp
    Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesInRangeAsync(
        IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
    ```
    **Do not change the existing `ListClosedSlicesAsync(machineIds, DateOnly? date, ct)`** — it only supports a single exact calendar day (or all-time), which is exactly what Epic 3's dashboard drill-down still needs; Story 4.1 needs an arbitrary `(start, end)` instant window (a Week is 7 days, a Shift may be a few hours starting mid-day), which that signature cannot express. Implement in `src/OeeNew.Infrastructure/Persistence/DowntimeEventRepository.cs` by copying the existing method's filter/projection shape (`e.EndedAt != null && machineIds.Contains(e.MachineId)`, left-join to `ReasonCode`, project to `ClosedDowntimeSlice`) but filtering `e.StartedAt >= start && e.StartedAt < end` unconditionally instead of the optional `DateOnly` branch.
  - [x] `src/OeeNew.Application/Production/IQualityRejectRepository.cs`: add `Task<int> SumQuantityInRangeAsync(IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)`, implemented in `src/OeeNew.Infrastructure/Persistence/QualityRejectRepository.cs` the same way (filter `RecordedAt` against `[start, end)` instead of the optional `DateOnly`).
  - [x] Update both fake test doubles (`tests/OeeNew.Application.Tests/Production/FakeDowntimeEventRepository.cs`, `FakeQualityRejectRepository.cs`) to implement the new methods — filter the same in-memory collections by `StartedAt`/`RecordedAt` falling in `[start, end)`.
- [x] Task 5: Backend — `OeeReportQueryUseCase` tying Tasks 1–4 together, controller, DI, and a new authorization policy (AC: #1, #2, #3)
  - [x] New file `src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs`, constructor `(IMachineRepository machines, ILineRepository lines, IShiftScheduleRepository shiftSchedules, IDowntimeEventRepository downtimeEvents, IQualityRejectRepository qualityRejects)` — one public method:
    ```csharp
    Task<OeeReportResult> GetReportAsync(CallerScope scope, ReportPeriodType periodType, DateOnly referenceDate, Guid? shiftScheduleId, CancellationToken cancellationToken = default)
    ```
    Internally: resolve `(start, end, plannedSeconds)` per Task 2, resolve `machineIds` per Task 3, call `downtimeEvents.ListClosedSlicesInRangeAsync` + `qualityRejects.SumQuantityInRangeAsync`, apply Task 1's formula. Structure the period-resolution and machine-resolution steps as separate private methods (`ResolvePeriodAsync`, `ResolveMachinesAsync`) — Story 4.3 will call back into this same class and reuse both, exactly the way Story 3.2 added `GetReasonBreakdownAsync` to `LossBreakdownQueryUseCase` and reused its existing private `ResolveEquipmentAsync`/`ResolveAreaAsync` rather than duplicating them in a new class.
  - [x] New file `src/OeeNew.Api/Controllers/ReportsController.cs`:
    ```csharp
    public sealed record OeeReportResponse(string PeriodType, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd,
        double AvailabilityPercent, double PerformancePercent, double QualityPercent, double OeePercent,
        long AvailabilityLossSeconds, long PerformanceLossSeconds, long QualityLossSeconds, long UnattributedSeconds, int QualityRejectQuantity);

    [ApiController]
    [Authorize(Policy = "ReportsAccess")]
    public sealed class ReportsController(OeeReportQueryUseCase reportUseCase) : ControllerBase
    {
        [HttpGet("api/reports/oee")]
        public async Task<ActionResult<OeeReportResponse>> GetOeeReport(
            [FromQuery] ReportPeriodType periodType, [FromQuery] DateOnly referenceDate, [FromQuery] Guid? shiftScheduleId, CancellationToken cancellationToken)
        {
            if (periodType == ReportPeriodType.Shift == (shiftScheduleId is null))
            {
                return BadRequest(/* shiftScheduleId required iff periodType == Shift — envelope shape per Consistency Conventions: { code, message } */);
            }
            var result = await reportUseCase.GetReportAsync(User.GetCallerScope(), periodType, referenceDate, shiftScheduleId, cancellationToken);
            return Ok(/* map to OeeReportResponse */);
        }
    }
    ```
    Class-level `[Authorize(Policy = "ReportsAccess")]`, unlike `LossAnalyticsController`'s class-level `[Authorize]` (any role) — every Reports endpoint needs Manager/Viewer/Admin only (AC #3), not "any authenticated role" the way the Epic 3 dashboard pie chart does.
  - [x] `src/OeeNew.Api/Program.cs:168-169` currently defines only one authorization policy (`AdminOnly`, `RequireClaim(OeeClaimTypes.Role, "Admin")`). Add a second policy right below it, same shape: `.AddPolicy("ReportsAccess", policy => policy.RequireClaim(OeeClaimTypes.Role, "Admin", "Manager", "Viewer"))` (`RequireClaim` accepts multiple allowed values — Operator is the only role excluded, matching the IA table in `EXPERIENCE.md` and the sidebar's existing `SIDEBAR_MENU` entry for `/reports`, `web/oee-shell/src/app/core/layout/sidebar-menu.ts:12`, which already lists `roles: ['Admin', 'Manager', 'Viewer']`).
  - [x] Register in `Program.cs` alongside the other Analytics/Production registrations (`builder.Services.AddScoped<OeeReportQueryUseCase>();`).
- [x] Task 6: Frontend — real `ReportsPage` with period selector + shift picker, replacing the placeholder (AC: #1, #2, #3)
  - [x] `web/oee-shell/src/app/pages/reports/reports-page.ts` is currently a placeholder (`<h2>{{ 'nav.reports' | translate }}</h2><p>{{ 'placeholder.comingLater' | translate }}</p>`) — replace its body with the real report view. Follow the same self-contained-widget shape `web/oee-shell/src/app/pages/dashboard/loss-pie-chart.ts` established (own signals for selection state, own service, `computed()` for derived display data) rather than inventing a new pattern.
  - [x] New file `web/oee-shell/src/app/pages/reports/oee-report.service.ts`, same shape as `loss-analytics.service.ts` (`@Injectable({ providedIn: 'root' })`, `firstValueFrom(this.http.get<T>(url))`, `URLSearchParams` for query building):
    ```ts
    export type ReportPeriodType = 'Shift' | 'Day' | 'Week';
    export interface OeeReportDto { periodType: ReportPeriodType; periodStart: string; periodEnd: string;
      availabilityPercent: number; performancePercent: number; qualityPercent: number; oeePercent: number;
      availabilityLossSeconds: number; performanceLossSeconds: number; qualityLossSeconds: number; unattributedSeconds: number; qualityRejectQuantity: number; }
    getReport(periodType: ReportPeriodType, referenceDate: string, shiftScheduleId?: string): Promise<OeeReportDto> { ... } // GET /api/reports/oee
    ```
  - [x] Period selector (`p-select`, same `SelectModule` used by `loss-pie-chart.ts`) for Shift/Day/Week; a `p-datepicker` for `referenceDate` (reuse the `toDateParam`-style `yyyy-MM-dd` formatting helper pattern from `loss-analytics.service.ts:31-36`); when `periodType === 'Shift'`, show a second `p-select` populated from `GET api/master-data/sites/{siteId}/shift-schedules` (`ShiftSchedulesController`, existing since Story 1.3) for the currently-selected site — reuse `ScopeService.selectedSiteId` (`web/oee-shell/src/app/core/scope/scope.service.ts`) rather than adding a second independent site selector, since the topbar's Site/Line selector is already the app-wide "which site am I looking at" state (its own doc comment at lines 6-11 already anticipates Epic 4 reusing it).
  - [x] Display: Availability/Performance/Quality/OEE as percentages (multiply by 100, one decimal place is fine — no existing precedent to match here since this is the first percentage display in the app). No chart library requirement in the AC/UX docs for this story — a simple stat display (labels + numbers) is sufficient; don't add a new Chart.js widget speculatively. `QualityRejectQuantity` shown as a plain supplementary number, same framing as `LossBreakdownDto.qualityRejectQuantity` on the dashboard.
  - [x] Empty/zero handling: if `plannedTimeSeconds` resolves to an empty machine scope (Task 3's "empty scope → zeroed result"), all four percentages come back as `0` from the backend — render them as-is, no special empty-state widget needed for this story (contrast with Epic 3's pie chart, which has an `isEmpty()` empty-state because an all-zero pie chart is visually meaningless; a `0%`/`0%`/`0%`/`0%` numeric report is not ambiguous the same way).
  - [x] i18n: add a new top-level `reports` namespace to `web/oee-shell/public/i18n/en.json`/`vi.json` (no `reports` key exists yet — current top-level keys are `nav`, `login`, `placeholder`, `dashboard`, `lossChart`, `masterData`). Suggested keys: `reports.title`, `reports.periodType.{shift,day,week}`, `reports.selectShift`, `reports.availability`, `reports.performance`, `reports.quality`, `reports.oee`, `reports.qualityReject`. Reuse `masterData.error.*` keys for error envelopes where the shape matches (e.g. `forbidden`) rather than duplicating them under `reports`.
- [x] Task 7: Frontend — deny Operator access to the Reports screen itself, not just hide the sidebar link (AC #3)
  - [x] The sidebar already excludes Operator from `/reports` (`SIDEBAR_MENU`'s `roles: ['Admin', 'Manager', 'Viewer']` entry, pre-existing) — but nothing stops an Operator from navigating to `/reports` directly by URL today. `deferred-work.md` accepted this same gap for `/master-data` project-wide ("No client-side per-route role guard — only the sidebar hides links... server-side enforcement is the real boundary per NFR-5") — that acceptance stands for `/master-data`, but this story's own AC #3 explicitly requires the Reports *screen* to reject Operator, so don't rely on the accepted master-data gap here; add the guard this AC asks for.
  - [x] New file `web/oee-shell/src/app/core/auth/role.guard.ts`, a small factory (not a fix to the master-data gap — scope this to what AC #3 needs): `export function roleGuard(allowedRoles: string[]): CanActivateFn { ... }` reading `AuthService.role()` (`web/oee-shell/src/app/core/auth/auth.service.ts:36`) and `Router.parseUrl('/dashboard')` (redirect, consistent with the existing catch-all `{ path: '**', redirectTo: 'dashboard' }` in `app.routes.ts`) when the role isn't in the allowed list; `true` otherwise. Apply it to the `/reports` route only: `{ path: 'reports', component: ReportsPage, canActivate: [roleGuard(['Admin', 'Manager', 'Viewer'])] }` in `web/oee-shell/src/app/app.routes.ts`.
- [x] Task 8: Testing (all AC)
  - [x] New file `tests/OeeNew.Application.Tests/Reports/OeeReportQueryUseCaseTests.cs`: period-resolution correctness for each `ReportPeriodType` (Day = exact UTC calendar day boundaries; Week = Monday–Monday ISO week containing an arbitrary mid-week `referenceDate`; Shift = combines a seeded `ShiftSchedule`'s `StartTime`/`EndTime` with `referenceDate`, including one overnight-wrap case e.g. 22:00–06:00); the divide-by-zero-guard case (100% Availability loss → Performance%/Quality% both `0`, not `NaN`); the unattributed-seconds-excluded-from-all-three-sums case; a Shift picked outside the caller's scope throws `MasterDataForbiddenException`; an empty-scope caller gets an all-zero result, not an exception.
  - [x] New fake `tests/OeeNew.Application.Tests/MasterData/FakeRepositories.cs` (extend, doesn't currently have one — confirmed no fake ShiftSchedule repository exists in the tree) — add `FakeShiftScheduleRepository` implementing `IShiftScheduleRepository`, same `Dictionary<Guid, T>`-backed shape as `FakeMachineStateRepository` (`tests/OeeNew.Application.Tests/Production/`).
  - [x] Extend `tests/OeeNew.Application.Tests/Production/FakeDowntimeEventRepository.cs`/`FakeQualityRejectRepository.cs` with the new range-query methods (Task 4).
  - [x] New file `tests/OeeNew.Api.Tests/Reports/ReportsEndpointsTests.cs`, following `tests/OeeNew.Api.Tests/Analytics/LossAnalyticsEndpointsTests.cs`'s real-Postgres full-flow pattern via `MasterDataApiFactory.CreateTokenFor(role, siteIds, lineIds)`: an Operator token gets 403/`FORBIDDEN` from `GET /api/reports/oee` (AC #3, backend boundary — the authoritative one per NFR-5); a Manager/Viewer/Admin token gets 200 with a correctly-computed report for a seeded Day period; a Shift-period request against a real seeded `ShiftSchedule` produces the expected window.
  - [x] `web/oee-shell/src/app/pages/reports/reports-page.spec.ts` (new) and `role.guard.spec.ts` (new): period/shift selection re-fetches the report; an Operator role hitting `/reports` directly is redirected (guard test, plain function call per the jsdom/canvas testing-boundary precedent already established in `loss-pie-chart.spec.ts` — no need for a real router harness if a direct `CanActivateFn` invocation suffices).

## Dev Notes

- **The OEE formula is the single highest-risk decision in this story — it was confirmed with the project owner this session, not silently assumed.** No `ProductionCount` entity, no ideal-cycle-time/target-rate field, and no counter history exist anywhere in the codebase (`MachineState` only ever holds the *latest* reading). Building a textbook count-based OEE (Performance = actual/ideal output, Quality = good/total count) would require a whole new master-data field and a new time-series entity — out of scope for this story. The chosen time-based-loss proxy reuses 100% existing data (`DowntimeEvent` + `ReasonCode.LossCategory`, exactly what Epic 3's pie chart already sums) and needs zero new master data. If a future story adds real ideal-cycle-time/count data, this formula will need revisiting — don't treat it as immutable, but don't second-guess it mid-implementation either.
- **`PlannedTimeSeconds` for Day/Week is the full UTC calendar period (86400s / 604800s), not shift-bounded.** There is no per-site "operating hours" or "which shifts run on which days" resolution anywhere in master data (`ShiftSchedule` doesn't say which days of the week it applies), so computing "planned production time" as "sum of shifts scheduled on this day" isn't derivable without inventing another assumption on top of this one. Keeping Day/Week measured against full calendar time is simpler and matches Epic 3's existing precedent of never excluding off-shift hours from a day's downtime sum.
- **Reuse, don't fork, `LossBreakdownQueryUseCase`'s scope-check reasoning.** `ResolveEquipmentAsync`/`ResolveAreaAsync` (`src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs:83-105`) are the canonical "client-supplied id is spoofable, always check against `CallerScope` before trusting it" pattern in this codebase — `OeeReportQueryUseCase`'s Shift-period machine resolution (Task 3) follows the exact same shape, just composed across possibly-multiple Lines for a site-wide shift instead of a single Line/Machine.
- **New authorization policy, not a new pattern.** `Program.cs` has exactly one policy today (`AdminOnly`). `ReportsAccess` is the same `RequireClaim` shape with three allowed values instead of one — don't introduce a different authorization mechanism (e.g. a custom `IAuthorizationHandler`) for this.
- **The client-side route guard is new to the codebase, deliberately scoped narrowly.** `deferred-work.md` explicitly accepted "sidebar-only" gating as sufficient for `/master-data` — that acceptance is not being revisited here. This story adds a guard only because its own AC #3 explicitly demands screen-level denial, not because the master-data gap needs fixing. Don't generalize this into "add guards to every route" as a drive-by improvement.
- **`OeeReportQueryUseCase` is a new class in a new `OeeNew.Application/Reports/` folder, not an addition to `Analytics/`.** The architecture spine's source tree deliberately separates `web/oee-shell/src/app/dashboard/` (FR-004..007, FR-019..021 — Epic 2/3) from `web/oee-shell/src/app/reports/` (FR-016..018 — Epic 4); mirror that split on the backend rather than growing `LossBreakdownQueryUseCase` (a dashboard/pie-chart concern) into a second, unrelated responsibility.

### Project Structure Notes

- New backend folder: `src/OeeNew.Application/Reports/` (`OeeReportQueryUseCase.cs`, `OeeReportResult.cs`, `ReportPeriodType.cs`), new controller `src/OeeNew.Api/Controllers/ReportsController.cs` — mirrors the existing `Analytics/` + `LossAnalyticsController.cs` pairing.
- `IDowntimeEventRepository`/`IQualityRejectRepository` gain a range-query method each; existing `DateOnly?`-based methods are untouched (Epic 3 still depends on them).
- New frontend files: `web/oee-shell/src/app/pages/reports/oee-report.service.ts`, `web/oee-shell/src/app/core/auth/role.guard.ts`. `reports-page.ts` is modified in place (placeholder → real).
- No DB schema changes — this story is pure aggregation over existing tables (`DowntimeEvent`, `ReasonCode`, `QualityReject`, `ShiftSchedule`), no new migration.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-4] — Story 4.1 full AC (FR-016)
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#4.5] — FR-016/017/018, and 4.2's FR-004 "chỉ số OEE" mention that Epic 2 never actually built
- [Source: src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs] — scope-resolution pattern reused (`ResolveEquipmentAsync`/`ResolveAreaAsync`), and the exact "seconds lost per LossCategory" data this story's formula is built on
- [Source: src/OeeNew.Application/Production/IDowntimeEventRepository.cs, src/OeeNew.Infrastructure/Persistence/DowntimeEventRepository.cs] — `ClosedDowntimeSlice`/`ListClosedSlicesAsync` being extended with a range overload
- [Source: src/OeeNew.Domain/MasterData/ShiftSchedule.cs] — `StartTime`/`EndTime` as `TimeOnly`, and the overnight-wrap handling in `OverlapsWith`/`ToMinuteSegments` this story's Shift-window resolution follows
- [Source: src/OeeNew.Api/Program.cs:168-169] — existing `AdminOnly` policy shape, extended with a second `ReportsAccess` policy
- [Source: web/oee-shell/src/app/core/layout/sidebar-menu.ts] — existing Operator-excluded `/reports` sidebar entry, confirms the intended role set for `ReportsAccess`
- [Source: web/oee-shell/src/app/core/scope/scope.service.ts] — global Site/Line selector this story's Shift-picker's site context reuses
- [Source: web/oee-shell/src/app/pages/dashboard/loss-pie-chart.ts, loss-analytics.service.ts] — self-contained-widget/service pattern this story's `ReportsPage`/`OeeReportService` follow
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — the accepted client-side-route-guard gap for `/master-data` that this story's Task 7 deliberately does not generalize beyond `/reports`
- `System.Globalization.ISOWeek` (.NET base class library, available since .NET Core 3.0) — used for Week-period boundary resolution, no third-party dependency needed

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

None — no blocking failures during implementation; backend and frontend test suites (scoped to the affected areas, per the known pre-existing `ng test` full-suite OOM tracked in `epic2_review_followups` memory) passed on first stable run after two isolated test-authoring fixes (HttpTestingController query-param matching against a manually-built query string, and awaiting a microtask tick after `HttpTestingController.flush()` before asserting signal state).

### Completion Notes List

- Implemented the time-based-loss-proxy OEE formula exactly as specified (Availability×Performance×Quality over Shift/Day/Week), with the divide-by-zero guard returning 0 instead of NaN, verified by a dedicated unit test.
- `OeeReportQueryUseCase.ResolvePeriodAsync`/`ResolveMachinesAsync` are both `internal` (visible to the test assembly via normal xUnit project reference) and independently callable, ready for Story 4.3 to reuse per Dev Notes.
- Extended `IDowntimeEventRepository`/`IQualityRejectRepository` with range-query overloads without touching the existing `DateOnly?`-based methods Epic 3 still depends on; updated both EF Core implementations and both fake test doubles.
- New `ReportsAccess` authorization policy (Admin/Manager/Viewer) added alongside the existing `AdminOnly` policy in `Program.cs`; `ReportsController` is class-level `[Authorize(Policy = "ReportsAccess")]`.
- `ReportsPage` replaces the placeholder with a real period selector (Shift/Day/Week), a reference-date picker, and a shift picker (populated from the currently-selected Site via the existing `ScopeService`/`MasterDataService.listShiftSchedules`) — self-contained widget/service shape matching `LossPieChart`/`LossAnalyticsService` (Story 3.1).
- Added a new `roleGuard` (`core/auth/role.guard.ts`), scoped only to the `/reports` route per Task 7's Dev Notes — does not generalize to `/master-data`'s already-accepted sidebar-only gap.
- Backend: 145/145 `OeeNew.Application.Tests` and 81/81 `OeeNew.Api.Tests` pass (including new Reports-specific suites), no regressions.
- Frontend: new `reports-page.spec.ts` (7 tests) and `role.guard.spec.ts` (3 tests) pass; re-ran `core/**`, `master-data/**`, and `loss-pie-chart.spec.ts` in isolation to confirm no regressions (the pre-existing, unrelated `ng test` full-suite OOM on `dashboard-page.spec.ts` — tracked separately — made a single full-suite run impractical, consistent with prior sessions' findings).

### File List

**Backend — new:**
- `src/OeeNew.Application/Reports/ReportPeriodType.cs`
- `src/OeeNew.Application/Reports/OeeReportResult.cs`
- `src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs`
- `src/OeeNew.Api/Controllers/ReportsController.cs`
- `tests/OeeNew.Application.Tests/Reports/OeeReportQueryUseCaseTests.cs`
- `tests/OeeNew.Api.Tests/Reports/ReportsEndpointsTests.cs`

**Backend — modified:**
- `src/OeeNew.Application/Production/IDowntimeEventRepository.cs`
- `src/OeeNew.Infrastructure/Persistence/DowntimeEventRepository.cs`
- `src/OeeNew.Application/Production/IQualityRejectRepository.cs`
- `src/OeeNew.Infrastructure/Persistence/QualityRejectRepository.cs`
- `src/OeeNew.Api/Program.cs`
- `tests/OeeNew.Application.Tests/Production/FakeDowntimeEventRepository.cs`
- `tests/OeeNew.Application.Tests/Production/FakeQualityRejectRepository.cs`

**Frontend — new:**
- `web/oee-shell/src/app/pages/reports/oee-report.service.ts`
- `web/oee-shell/src/app/core/auth/role.guard.ts`
- `web/oee-shell/src/app/pages/reports/reports-page.spec.ts`
- `web/oee-shell/src/app/core/auth/role.guard.spec.ts`

**Frontend — modified:**
- `web/oee-shell/src/app/pages/reports/reports-page.ts`
- `web/oee-shell/src/app/app.routes.ts`
- `web/oee-shell/public/i18n/en.json`
- `web/oee-shell/public/i18n/vi.json`

## Change Log

- 2026-07-22: Story 4.1 implemented end-to-end (backend OEE aggregation + API + frontend report page + Operator route guard). All tasks/subtasks complete, all ACs satisfied, backend and frontend tests green. Status: ready-for-dev → review.
