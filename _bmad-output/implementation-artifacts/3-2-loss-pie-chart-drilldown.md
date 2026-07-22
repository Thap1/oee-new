---
baseline_commit: 9d772bef07e59d319b5acb08bc8e21945a7e99ba
---

# Story 3.2: Drill-down pie chart theo ngày & theo mã lý do

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to pick a specific date and tap into a pie slice,
so that I can inspect that day's loss breakdown by reason code without it blending into a broader range.

## Acceptance Criteria

1. **Given** pie chart đang hiển thị tổng hợp nhiều ngày **When** tôi chọn một ngày cụ thể qua date-picker **Then** pie chart chỉ hiển thị dữ liệu tổn thất của riêng ngày đó (FR-021)
2. **Given** tôi chạm/click vào một lát pie chart (vd Availability) **When** thao tác **Then** hiển thị breakdown theo từng mã lý do dừng máy góp phần vào lát đó, không cần menu phụ (UX-DR8) — `[ASSUMPTION]` giải quyết câu hỏi mở FR-019: mức tổng ở pie chart là 3 loss category, drill-down theo lát mới xuống mã lý do (đã xác nhận ở epics.md)
3. **Given** không có dữ liệu tổn thất cho ngày được chọn **When** pie chart render **Then** hiển thị empty state rõ ràng thay vì biểu đồ trống khó hiểu

## Tasks / Subtasks

- [x] Task 1: Backend — confirm the date filter Story 3.1 already built end-to-end (AC: #1)
  - [x] **This is a verification task, not new production code.** Story 3.1's `LossAnalyticsController.GetBreakdown` already accepts `[FromQuery] DateOnly? date` and passes it straight through to `LossBreakdownQueryUseCase.GetAsync` → `IDowntimeEventRepository.ListClosedSlicesAsync(machineIds, date, ct)` / `IQualityRejectRepository.SumQuantityAsync(machineIds, date, ct)`, both of which already filter by UTC calendar date when `date` is non-null. Story 3.1 just never exercised the non-null path (always called with `date: null`). Before writing new code, read `src/OeeNew.Infrastructure/Persistence/DowntimeEventRepository.cs`'s `ListClosedSlicesAsync` and confirm this — if it turns out `date` filtering doesn't actually work end-to-end, that's a Story 3.1 bug to fix at its source, not something to patch around here.
  - [x] Add the missing test proving it: `tests/OeeNew.Api.Tests/Analytics/LossAnalyticsEndpointsTests.cs` (extend) — seed two closed DowntimeEvents on different calendar days (`StartedAt` on day 1 vs. day 2), call `loss-breakdown?...&date={day1}`, assert only day 1's duration is counted.
- [x] Task 2: Backend — reason-code breakdown within a loss category (AC: #2)
  - [x] `src/OeeNew.Application/Production/IDowntimeEventRepository.cs`: add `ReasonCodeId` to the `ClosedDowntimeSlice` record → `ClosedDowntimeSlice(Guid MachineId, Guid? ReasonCodeId, LossCategory? LossCategory, long DurationSeconds)`. **Note the invariant:** by construction (the `ReasonCode` FK is RESTRICT and never hard-deleted while referenced — Story 2.5 AC #5), `LossCategory != null` if-and-only-if `ReasonCodeId != null` — a slice never has one without the other. Update the EF query's projection in `DowntimeEventRepository.ListClosedSlicesAsync` to also select `e.ReasonCodeId`, and update `FakeDowntimeEventRepository.SeedClosed`/`ListClosedSlicesAsync` (Story 3.1) to carry it through too. This is a superset change — `LossBreakdownQueryUseCase.GetAsync` (Story 3.1) ignores the new field and needs no changes.
  - [x] `src/OeeNew.Application/MasterData/IReasonCodeRepository.cs`: add `Task<IReadOnlyList<ReasonCode>> ListByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)` — same one-query-not-N pattern as `ILineRepository.ListByIdsAsync` (Story 2.5). Implement in `ReasonCodeRepository.cs` and the test double (`tests/OeeNew.Application.Tests/MasterData/FakeRepositories.cs`).
  - [x] `src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs`: add a second public method (same class — it already owns the exact target-resolution/scope-check logic this needs via its private `ResolveEquipmentAsync`/`ResolveAreaAsync` helpers built in Story 3.1; do not duplicate that logic in a new class):
    ```
    GetReasonBreakdownAsync(CallerScope scope, LossBreakdownTargetType targetType, Guid targetId, LossCategory lossCategory, DateOnly? date, CancellationToken)
    ```
    Resolve `machineIds` exactly like `GetAsync` does, call `ListClosedSlicesAsync(machineIds, date, ct)`, filter to `slice.LossCategory == lossCategory` (which per the invariant above always has `ReasonCodeId` set), group by `ReasonCodeId`, sum `DurationSeconds`, resolve names via `reasonCodes.ListByIdsAsync(...)`, return a new `IReadOnlyList<ReasonBreakdownItem>` (new record: `Guid ReasonCodeId, string ReasonCodeName, long DurationSeconds`) ordered by `DurationSeconds` descending (largest contributor first — matches Story 4.3's "top reason" framing elsewhere in this PRD). Add `IReasonCodeRepository reasonCodes` to the constructor.
  - [x] `src/OeeNew.Api/Controllers/LossAnalyticsController.cs`: add `GET /api/analytics/loss-breakdown/reasons?targetType=&targetId=&lossCategory=&date=` calling `GetReasonBreakdownAsync`. New response record `ReasonBreakdownItemResponse(Guid ReasonCodeId, string ReasonCodeName, long DurationSeconds)`.
- [x] Task 3: Frontend — date-picker + slice-tap drill-down (AC: #1, #2, #3)
  - [x] Add a PrimeNG `DatePicker` (`import { DatePicker } from 'primeng/datepicker'`, selector `p-datepicker` — confirmed via `node_modules/primeng/types/primeng-datepicker.d.ts`; NOT the deprecated `Calendar` name) to `loss-pie-chart.ts`'s controls row, bound to a new `selectedDate: Date | null` signal. `null` means "no filter" (Story 3.1's all-time default, matching AC #1's premise that the chart starts showing multi-day aggregate before a date is picked).
  - [x] `loss-analytics.service.ts`: extend `getBreakdown` to accept an optional `date?: string` (ISO `yyyy-MM-dd`) query param, and add `getReasonBreakdown(targetType, targetId, lossCategory, date?): Promise<ReasonBreakdownDto[]>` (`GET /api/analytics/loss-breakdown/reasons`).
  - [x] `loss-pie-chart.ts`: selecting a date re-fetches the current target's breakdown with that date (re-run the exact same `selectTarget`-style fetch, just with the date param included — don't duplicate the fetch logic). Wire `<p-chart ... (onDataSelect)="onSliceSelected($event)">` — `event.element.index` is the clicked dataset point index (0/1/2 = Availability/Performance/Quality, confirmed from `primeng-chart.mjs`'s `onCanvasClick` implementation: `this.onDataSelect.emit({ originalEvent, element: element[0], dataset })`). Map index → `LossCategory` enum value, call `getReasonBreakdown`, render the result as an inline list directly below the chart (UX-DR8: "không cần menu phụ" — no popover/modal, just inline).
  - [x] Empty state (AC #3): the existing `isEmpty()` computed from Story 3.1 already covers "selected date has zero loss seconds" with no changes — a date with no closed downtime naturally produces an all-zero breakdown response. Don't add a second, date-specific empty-state path.
  - [x] i18n: add `lossChart.selectDate`, `lossChart.reasonBreakdown.title`, `lossChart.reasonBreakdown.empty` (defensive — see Dev Notes on why this branch is expected to be unreachable, but still needs a message rather than a blank list if it ever is) to `en.json`/`vi.json`.
- [x] Task 4: Testing (all AC)
  - [x] `tests/OeeNew.Application.Tests/Analytics/LossBreakdownQueryUseCaseTests.cs` (extend): `GetAsync` with a non-null `date` only counts events on that UTC day, excluding an otherwise-identical event on a different day (AC #1, closes the gap Task 1 identified); `GetReasonBreakdownAsync` groups multiple events under the same ReasonCode into one summed item, filters out events from a different `LossCategory`, and returns items ordered by seconds descending; a `lossCategory` with zero matching events returns an empty list.
  - [x] `tests/OeeNew.Api.Tests/Analytics/LossAnalyticsEndpointsTests.cs` (extend): full-flow test — two closed, differently-reasoned Availability events on the same machine/day → `loss-breakdown/reasons?...&lossCategory=AvailabilityLoss` returns both reason codes with correct per-reason totals.
  - [x] `web/oee-shell/src/app/pages/dashboard/loss-pie-chart.spec.ts` (extend): selecting a date re-fetches breakdown with the date query param; calling `onSliceSelected({ element: { index: 0 }, ... })` directly (see Dev Notes — jsdom has no canvas, so this can't be triggered via a real chart click) fetches and exposes the Availability reason breakdown.

## Dev Notes

- **Task 1 is real but small on purpose — same "confirm, don't rebuild" shape as Story 2.4.** Story 3.1 designed the `date: DateOnly?` parameter through every layer (repository interfaces, use case, controller) specifically so this story wouldn't need to touch the interface again — only the value being non-null is new. If Task 1's read-through reveals the filtering doesn't actually work, stop and fix it in Story 3.1's own files (`IDowntimeEventRepository`/`DowntimeEventRepository`), not with a workaround here.
- **The `LossCategory != null ⟺ ReasonCodeId != null` invariant is load-bearing for Task 2.** It's why `GetReasonBreakdownAsync` can group by `ReasonCodeId` after filtering by `LossCategory` without a separate null-check producing a "some reason codes silently dropped" bug. It holds because `ReasonCode` rows are never hard-deleted while any `DowntimeEvent` still references them (Story 1.5 AC #2 + Story 2.5 AC #5's guard via `IDowntimeEventRepository.ExistsForReasonCodeAsync`). Don't add extra defensive filtering "just in case" — that would silently hide a real bug elsewhere if the invariant ever breaks, rather than surfacing it.
- **Reuse `LossBreakdownQueryUseCase`'s existing private scope-check helpers for the new method — do not create a second use case class.** `ResolveEquipmentAsync`/`ResolveAreaAsync` (Story 3.1) already do exactly the target-resolution + scope-enforcement this drill-down needs; a separate class would either duplicate that logic or need it exposed as `internal`/public just for reuse, both worse than one class with two public methods.
- **jsdom in this project has no real `<canvas>` 2D context** (no `canvas` npm package installed — see Story 3.1 Dev Notes/Completion Notes). `UIChart`'s `onCanvasClick` calls `this.chart.getElementsAtEventForMode(...)`, which needs a real Chart.js instance bound to a working canvas — this will not function under a simulated DOM click in `ng test`. Test `onSliceSelected(...)` as a plain method call with a hand-built event object (`{ element: { index: 0 }, dataset: {...}, originalEvent: {} }`), the same boundary Story 3.1 already drew around `chartData()` to avoid ever mounting a real chart with non-empty data in a spec.
- **Don't add a modal/popover for the reason breakdown.** UX-DR8 and the epics.md AC text both say "không cần menu phụ" (no secondary menu) — render the list inline in the same widget, replacing/appending to what's already below the chart. If this starts feeling like it needs its own routed view or overlay, that's over-building past what's asked.

### Project Structure Notes

- No new folders. All backend changes extend Story 3.1's `OeeNew.Application/Analytics/` and `LossAnalyticsController.cs`; all frontend changes extend Story 3.1's `loss-pie-chart.ts`/`loss-analytics.service.ts` in `web/oee-shell/src/app/pages/dashboard/`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-3] — Story 3.2 full AC, including the `[ASSUMPTION]` resolving FR-019's open question (3-category pie, reason-code drill-down per slice)
- [Source: _bmad-output/implementation-artifacts/3-1-loss-pie-chart-view.md] — everything this story extends: `ClosedDowntimeSlice`, `LossBreakdownQueryUseCase`, `LossAnalyticsController`, `loss-pie-chart.ts`, the jsdom/canvas testing limitation, the "no auto-select on load" decision
- [Source: src/OeeNew.Application/MasterData/ILineRepository.cs] — `ListByIdsAsync` pattern reused for the new `IReasonCodeRepository.ListByIdsAsync`
- PrimeNG `DatePicker` (`node_modules/primeng/types/primeng-datepicker.d.ts`, selector `p-datepicker`/`p-datePicker`/`p-date-picker`) and `UIChart.onDataSelect` payload shape (`node_modules/primeng/fesm2022/primeng-chart.mjs` — `onCanvasClick`) — verified directly against the installed `primeng@21.1.9`, do not assume an older-version API shape.

### Review Findings

- [x] [Review][Defer] Date filter uses local browser day on the frontend but a UTC calendar day on the backend — for a VN-timezone user this can shift the selected day's window by up to 7 hours. User decision (2026-07-22): accept as a known MVP limitation — no site-timezone concept exists anywhere in the domain yet, and inventing one is real scope creep beyond a quick fix. Revisit if/when the domain model gains a site-timezone field. [loss-analytics.service.ts `toDateParam`, DowntimeEventRepository.cs `ListClosedSlicesAsync`]
- [x] [Review][Patch] No test proves the reason-breakdown endpoint's scope check actually fires via HTTP — `LossAnalyticsEndpointsTests` has a forbidden test for `/api/analytics/loss-breakdown` but none for `/api/analytics/loss-breakdown/reasons` [tests/OeeNew.Api.Tests/Analytics/LossAnalyticsEndpointsTests.cs] — fixed: added `GetReasonBreakdown_TargetOutsideCallerScope_ReturnsForbidden`

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia — BMad dev agent)

### Debug Log References

- Backend: `dotnet test` (full solution) — Domain 68/68, Application 138/138, Api 72/72, Architecture 2/2 (280/280, all green; no regressions from Story 3.1's 275/275).
- `npx tsc -p tsconfig.spec.json --noEmit` — clean.
- `npx ng build` — succeeds (initial bundle ~1.56MB, same as Story 3.1; no further budget change needed since `DatePicker` was already bundled inside the shared PrimeNG package, no new heavy dependency added).
- `ng test`: same standing OOM issue as Story 3.1 (see `epic2_review_followups` memory) — verified the new/changed frontend work via isolated `--include` runs instead: `loss-pie-chart.spec.ts` alone — 9/9 pass (5 from Story 3.1 + 4 new).

### Completion Notes List

- Task 1 confirmed Story 3.1's `date: DateOnly?` plumbing already worked end-to-end with no code changes needed — only a missing test (AC #1) was added, both at the `LossBreakdownQueryUseCase` level (fake repos) and as a real Postgres-backed API integration test.
- Task 2 added `ReasonCodeId` to `ClosedDowntimeSlice` and a new `LossBreakdownQueryUseCase.GetReasonBreakdownAsync` method (same class as Story 3.1's `GetAsync`, reusing its private `ResolveEquipmentAsync`/`ResolveAreaAsync` scope-check helpers verbatim — no new use-case class). Relies on the documented invariant that `LossCategory` is non-null iff `ReasonCodeId` is non-null (ReasonCode rows are never hard-deleted while referenced, Story 2.5 AC #5), so grouping by `ReasonCodeId` after filtering by category needed no extra null-handling.
- Frontend: added a PrimeNG `DatePicker` and wired `UIChart`'s `onDataSelect` to a `onSliceSelected` handler that maps the clicked dataset index (0/1/2) to `AvailabilityLoss`/`PerformanceLoss`/`QualityLoss` and fetches/renders that category's reason breakdown inline below the chart (no popover/modal, per UX-DR8's "không cần menu phụ"). Switching target or date clears any shown reason breakdown (it would otherwise reference a slice from a different chart state).
- Deliberately did not add a date-specific empty state — Story 3.1's existing all-zero-categories `isEmpty()` check already covers "no downtime on the selected date" with zero new code, since the API naturally returns all-zero seconds for a date with nothing closed.
- `ng test`'s jsdom has no real `<canvas>` context (no `canvas` npm package), so `onSliceSelected` — which in production only fires from a real Chart.js click — is tested by calling it directly with a hand-built `{ element: { index } }` event, the same testing boundary Story 3.1 established for `chartData()`.

### File List

**Backend — modified:**
- `src/OeeNew.Application/Production/IDowntimeEventRepository.cs` (`ClosedDowntimeSlice` +`ReasonCodeId`)
- `src/OeeNew.Infrastructure/Persistence/DowntimeEventRepository.cs` (projection +`ReasonCodeId`)
- `src/OeeNew.Application/MasterData/IReasonCodeRepository.cs` (+`ListByIdsAsync`)
- `src/OeeNew.Infrastructure/Persistence/ReasonCodeRepository.cs` (+`ListByIdsAsync` EF implementation)
- `src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs` (+`GetReasonBreakdownAsync`, +`IReasonCodeRepository` dependency)
- `src/OeeNew.Api/Controllers/LossAnalyticsController.cs` (+`GET /api/analytics/loss-breakdown/reasons`, +`ReasonBreakdownItemResponse`)
- `tests/OeeNew.Application.Tests/Production/FakeDowntimeEventRepository.cs` (`SeedClosed` +`ReasonCodeId` overload, `ListClosedSlicesAsync` carries it through)
- `tests/OeeNew.Application.Tests/MasterData/FakeRepositories.cs` (`FakeReasonCodeRepository` +`ListByIdsAsync`)
- `tests/OeeNew.Application.Tests/Analytics/LossBreakdownQueryUseCaseTests.cs` (+date-filter test, +`GetReasonBreakdownAsync` tests, all existing use-case constructions updated for the new `reasonCodes` dependency)
- `tests/OeeNew.Api.Tests/Analytics/LossAnalyticsEndpointsTests.cs` (+date-filter full-flow test, +reason-breakdown full-flow test; review-fix: +`GetReasonBreakdown_TargetOutsideCallerScope_ReturnsForbidden`)

**Backend — new:**
- `src/OeeNew.Application/Analytics/ReasonBreakdownItem.cs`

**Frontend — new:**
- (none — all Story 3.2 frontend work extends Story 3.1's existing files)

**Frontend — modified:**
- `web/oee-shell/src/app/pages/dashboard/loss-analytics.service.ts` (+`ReasonBreakdownDto`, `getBreakdown` +`date` param, +`getReasonBreakdown`)
- `web/oee-shell/src/app/pages/dashboard/loss-pie-chart.ts` (+`DatePicker`, +`onDateChange`/`onSliceSelected`, +reason-breakdown signals/template section)
- `web/oee-shell/src/app/pages/dashboard/loss-pie-chart.spec.ts` (+date-filter tests, +slice-selection tests, +target-switch-clears-breakdown test)
- `web/oee-shell/public/i18n/en.json`, `public/i18n/vi.json` (+`lossChart.selectDate`, +`lossChart.reasonBreakdown.*`)

## Change Log

- 2026-07-22: Implemented Story 3.2 end-to-end — confirmed Story 3.1's date filter already worked (added the missing test), added the reason-code drill-down endpoint/use-case method, and wired a date-picker + tap-to-drill-down into the Dashboard's Loss Pie Chart widget. Backend 280/280 tests passing (no regressions). Frontend type-checks and builds clean; `ng test` verified via isolated per-file run (`loss-pie-chart.spec.ts`: 9/9 pass) due to the pre-existing environment OOM issue. Status → review.
- 2026-07-22: Code review (bmad-code-review) found the local-day/UTC-day date filter mismatch (frontend sends local calendar day, backend filters UTC) — user decided to accept as a known MVP limitation rather than invent a site-timezone concept. Also found and fixed a missing scope-forbidden test for `/api/analytics/loss-breakdown/reasons`. Backend re-verified 282/282. Status → done.
