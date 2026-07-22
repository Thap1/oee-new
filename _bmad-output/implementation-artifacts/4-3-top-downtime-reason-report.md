---
baseline_commit: 9ed4738da848404782bd3f29958b1547190f72a8
---

# Story 4.3: Xem nguyên nhân dừng máy chiếm nhiều thời gian nhất

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Manager,
I want the report to surface the top downtime reason for the period,
so that I can bring it directly into the shift handover meeting without compiling Excel manually.

## Acceptance Criteria

1. **Given** báo cáo đã được tạo cho một kỳ (ca/ngày/tuần) **When** tôi xem report **Then** hệ thống hiển thị mã lý do dừng máy chiếm nhiều thời gian nhất trong kỳ đó, kèm tổng thời gian (FR-018)
2. **Given** nhiều mã lý do có tổng thời gian bằng nhau **When** xếp hạng **Then** hiển thị theo thứ tự ổn định (vd. theo tên) để không gây nhầm lẫn giữa các lần xem `[ASSUMPTION]`
3. **Given** không có DowntimeEvent nào trong kỳ **When** report render **Then** hiển thị "Không có dữ liệu dừng máy" thay vì lỗi hoặc trống khó hiểu

## Tasks / Subtasks

- [x] Task 1: Backend — compute the top downtime reason from data `GetReportAsync` already fetches, don't add a second query (AC: #1, #2, #3)
  - [x] **Design choice, not an oversight: this is bundled into the existing `GetReportAsync` response, not a separate endpoint** (unlike Epic 3's `loss-breakdown/reasons`, which is a genuinely separate on-demand drill-down triggered by a pie-slice tap). AC #1's framing is "when I view the report" — shown immediately, not behind an interaction — and `GetReportAsync` already calls `downtimeEvents.ListClosedSlicesInRangeAsync(machineIds, start, end, ct)` (Story 4.1) for the exact same machine set and time window this story needs. Re-querying that same data through a second endpoint/DB round-trip would be pure waste — reuse the in-memory `slices` list already fetched inside `GetReportAsync`.
  - [x] Extend `OeeReportResult` (`src/OeeNew.Application/Reports/OeeReportResult.cs`, Story 4.1) with three new nullable fields: `Guid? TopDowntimeReasonCodeId, string? TopDowntimeReasonName, long? TopDowntimeReasonSeconds` — all `null` together when no attributed downtime exists in the period (AC #3), never partially null.
  - [x] `OeeReportQueryUseCase`'s constructor (`src/OeeNew.Application/Reports/OeeReportQueryUseCase.cs`) gains an `IReasonCodeRepository reasonCodes` dependency (already registered in DI, `Program.cs:66` — no new DI registration needed, just add the constructor parameter). Inside `GetReportAsync`, after computing the Availability/Performance/Quality sums from `slices` (Story 4.1 Task 1), also:
    ```csharp
    // Same invariant Story 3.2 documented: LossCategory != null iff ReasonCodeId != null (ReasonCode rows are
    // never hard-deleted while referenced — Story 1.5 AC #2 + Story 2.5 AC #5). Unlike
    // LossBreakdownQueryUseCase.GetReasonBreakdownAsync, this groups across ALL three categories together,
    // not one — "top downtime reason" in the AC has no category restriction.
    var totalsByReasonCodeId = slices
        .Where(s => s.ReasonCodeId is not null)
        .GroupBy(s => s.ReasonCodeId!.Value)
        .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationSeconds));

    (Guid Id, string Name, long Seconds)? top = null;
    if (totalsByReasonCodeId.Count > 0)
    {
        var matchingReasonCodes = await reasonCodes.ListByIdsAsync(totalsByReasonCodeId.Keys.ToList(), cancellationToken);
        top = matchingReasonCodes
            .Select(r => (r.Id, r.Name, Seconds: totalsByReasonCodeId[r.Id]))
            .OrderByDescending(t => t.Seconds)
            .ThenBy(t => t.Name, StringComparer.Ordinal) // AC #2: stable tie-break by name
            .FirstOrDefault();
    }
    ```
    Map `top` into the three new `OeeReportResult` fields (all `null` when `top` is `null` — AC #3).
  - [x] `src/OeeNew.Api/Controllers/ReportsController.cs`: add the three fields to `OeeReportResponse` and the mapping in `GetOeeReport` — no new route.
- [x] Task 2: Backend — update every existing call site from Stories 4.1/4.2 (constructor signature and result shape both changed) (AC: all)
  - [x] **This is the same kind of ripple Story 3.2 caused when it added `IReasonCodeRepository` to `LossBreakdownQueryUseCase`'s constructor** ("all existing use-case constructions updated for the new reasonCodes dependency" — Story 3.2 Completion Notes). Every test in `tests/OeeNew.Application.Tests/Reports/OeeReportQueryUseCaseTests.cs` (Stories 4.1/4.2) that constructs `OeeReportQueryUseCase` directly needs the new `reasonCodes` argument added; every assertion against a full `OeeReportResult` needs to account for the three new fields (or use a fake/builder that defaults them).
- [x] Task 3: Frontend — display the top reason inline with the report, with an explicit empty state (AC: #1, #3)
  - [x] `web/oee-shell/src/app/pages/reports/oee-report.service.ts` (Story 4.1)'s `OeeReportDto` gains `topDowntimeReasonCodeId: string | null`, `topDowntimeReasonName: string | null`, `topDowntimeReasonSeconds: number | null` — no service method changes, this rides on the existing `getReport` call (Task 1's bundling decision).
  - [x] `web/oee-shell/src/app/pages/reports/reports-page.ts` (Story 4.1): render the top reason directly below the OEE percentages (no click/drill-down — matches this story's "shown when viewing," contrast with `loss-pie-chart.ts`'s click-triggered `onSliceSelected`/`reasonBreakdown` section, which is a different, on-demand interaction from a different story). When `topDowntimeReasonName` is `null`, show the `reports.topDowntimeReason.empty` message ("Không có dữ liệu dừng máy," AC #3) instead of a blank area — same "always an explicit empty state, never a silent blank" pattern `dashboard`'s `emptyState` and `lossChart.emptyState` already establish elsewhere in this app.
  - [x] i18n: add `reports.topDowntimeReason.{title, empty}` to the `reports` namespace (`en.json`/`vi.json`, introduced Story 4.1).
- [x] Task 4: Testing (all AC)
  - [x] Extend `tests/OeeNew.Application.Tests/Reports/OeeReportQueryUseCaseTests.cs`: two reason codes with equal total durations in the same period → the one that sorts first by name (ordinal) wins, and the result is identical across repeated calls (AC #2, stability); a reason code tagged `PerformanceLoss` with more total time than one tagged `AvailabilityLoss` still wins — proves the grouping is NOT filtered by `LossCategory` (unlike `GetReasonBreakdownAsync`); a period with zero closed events, and separately a period with only unattributed (`ReasonCodeId == null`) events, both produce all-three-fields-`null` (AC #3); a normal case returns the correct top reason + seconds alongside correctly-unaffected Availability/Performance/Quality percentages (proving Task 1 didn't perturb Story 4.1/4.2's existing math).
  - [x] Extend `tests/OeeNew.Api.Tests/Reports/ReportsEndpointsTests.cs`: full-flow test seeding two differently-reasoned closed `DowntimeEvent`s on the same machine/period, asserting `GET /api/reports/oee` returns the correctly-summed top reason in one response (no second HTTP call).
  - [x] `web/oee-shell/src/app/pages/reports/reports-page.spec.ts` (extend): renders the top-reason name + seconds when present; renders the empty-state message when `topDowntimeReasonName` is `null`.

## Dev Notes

- **Bundled into the existing response by design, not an oversight to flag in review.** If a future reviewer asks "why isn't this a separate endpoint like Epic 3's reason breakdown," the answer is: Epic 3's is a click-triggered drill-down over a *single* pie slice's category (needs its own request because it's optional/on-demand and category-scoped); this story's requirement is unconditional ("khi tôi xem report") over data the main report call already fetches. Splitting it into a second endpoint would just add a redundant identical `ListClosedSlicesInRangeAsync` query for zero benefit.
- **Grouping spans all three `LossCategory` values together** — re-read AC #1 if tempted to filter by category; "nguyên nhân dừng máy chiếm nhiều thời gian nhất" has no category qualifier, unlike Story 3.2's per-slice reason breakdown which is deliberately scoped to one category at a time.
- **The tie-break comparer matters for stability across repeated views (AC #2's actual concern), not just for passing a test.** Use `StringComparer.Ordinal` (not the current culture's comparer) so tie-break order can't silently differ between a Vietnamese-locale and English-locale server process — the same reasoning `ClosedDowntimeSlice`/`ReasonBreakdownItem`'s existing `OrderByDescending` chains don't currently need to worry about (they never tie-break by string), but this story's does.
- **This story's Task 2 is a real, unavoidable ripple into 4.1/4.2's test files — budget time for it, don't treat it as scope creep.** Adding a constructor dependency to an already-multiply-tested class always does this; Story 3.2 hit the identical situation and it's a normal, expected part of extending a shared use-case class rather than forking a new one.

### Project Structure Notes

- No new files at all — every change in this story extends files Story 4.1 (and, where relevant, 4.2) already created: `OeeReportResult.cs`, `OeeReportQueryUseCase.cs`, `ReportsController.cs`, `oee-report.service.ts`, `reports-page.ts`, plus their respective test files.
- No DB schema changes.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-4] — Story 4.3 full AC (FR-018), including the `[ASSUMPTION]` tag on AC #2 already present in epics.md
- [Source: _bmad-output/implementation-artifacts/4-1-oee-report-shift-day-week.md] — `OeeReportResult`, `OeeReportQueryUseCase.GetReportAsync`, `ReportsController`, `ReportsPage`, `OeeReportService` — everything this story extends
- [Source: _bmad-output/implementation-artifacts/4-2-report-site-line-machine-filter.md] — confirms the filter parameters flow through to the same `slices` this story reuses (no interaction needed between the two stories' changes)
- [Source: src/OeeNew.Application/Analytics/LossBreakdownQueryUseCase.cs:49-81] — `GetReasonBreakdownAsync`, the closest existing precedent for "group closed slices by ReasonCodeId, resolve names via `IReasonCodeRepository.ListByIdsAsync`, order by seconds descending" — this story's top-reason computation is the same shape, minus the single-category filter, plus a name tie-break
- [Source: _bmad-output/implementation-artifacts/3-2-loss-pie-chart-drilldown.md] — precedent for "adding a repository dependency to an existing use case ripples into every existing test construction," directly reused in this story's Task 2/Dev Notes

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

None — no blocking failures. The constructor-signature ripple into `OeeReportQueryUseCaseTests.cs`'s `BuildUseCase` helper (Task 2) was handled by adding an optional `reasonCodes` parameter defaulting to a fresh `FakeReasonCodeRepository()`, so none of Stories 4.1/4.2's existing test bodies needed individual edits.

### Completion Notes List

- Computed the top downtime reason inside `GetReportAsync` by reusing the already-fetched `slices` list — no second query/endpoint, per the story's explicit design decision.
- Grouped across all three `LossCategory` values together (not filtered to one, unlike `LossBreakdownQueryUseCase.GetReasonBreakdownAsync`), confirmed by a dedicated test where a `PerformanceLoss`-tagged reason with more seconds beats an `AvailabilityLoss`-tagged one.
- Tie-break uses `StringComparer.Ordinal` on the reason name (AC #2), verified stable across two repeated calls in the same test.
- `OeeReportResult`'s three new fields (`TopDowntimeReasonCodeId/Name/Seconds`) default to `null` via optional trailing parameters, so the existing empty-scope/zero-result construction sites in `OeeReportQueryUseCase.GetReportAsync` needed no changes.
- `OeeReportQueryUseCase` gained an `IReasonCodeRepository` constructor dependency (already registered in DI) — updated `OeeReportQueryUseCaseTests.BuildUseCase` to accept an optional fake, the same ripple-handling pattern Story 3.2 used for `LossBreakdownQueryUseCase`.
- Frontend: `ReportsPage` renders the top-reason name + seconds directly below the OEE stats when present, or an explicit `reports.topDowntimeReason.empty` message when `topDowntimeReasonName` is `null` (AC #3) — no click/drill-down interaction, unlike Epic 3's pie-chart reason breakdown.
- Backend: 156/156 `OeeNew.Application.Tests`, 86/86 `OeeNew.Api.Tests` pass (5 new Application-level, 2 new Api-level tests for this story), no regressions.
- Frontend: `reports-page.spec.ts` extended to 12 tests (2 new, covering the present and empty top-downtime-reason states); re-ran `core/**`, `pages/master-data/**`, and `loss-pie-chart.spec.ts` in isolation to confirm no regressions (same pre-existing `ng test` full-suite OOM caveat as Stories 4.1/4.2).

### File List

**Backend — modified:**
- `src/OeeNew.Application/Reports/OeeReportResult.cs`
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

- 2026-07-22: Story 4.3 implemented — top downtime reason (all categories, name-tie-broken, explicit empty state) bundled into Story 4.1/4.2's existing `GetReportAsync` response, no new endpoint. All tasks/subtasks complete, all ACs satisfied, backend and frontend tests green. Status: ready-for-dev → review.
