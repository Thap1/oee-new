---
baseline_commit: 153bffe33e1cfaa4730411a270388f54b0c1bd21
---

# Story 2.4: Dashboard tổng hợp nhiều máy/line theo phân quyền

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Manager/Viewer,
I want to see a dashboard of multiple machines/lines at once, limited to my assigned site/line scope,
so that I can monitor my area without seeing unrelated sites.

## Acceptance Criteria

1. **Given** tôi là Manager/Viewer có quyền trên Line A và B **When** mở dashboard **Then** chỉ thấy máy thuộc Line A/B (FR-006, dùng scope đã enforce ở Story 1.6)
2. **Given** tôi chưa được gán máy nào **When** dashboard tải **Then** hiển thị empty state hướng dẫn liên hệ Admin (UX-DR10), không để trắng trơn
3. **Given** tôi gọi API xin dữ liệu máy ngoài phạm vi **When** request được gửi **Then** bị từ chối (FR-015, NFR-5)

## Tasks / Subtasks

- [x] Task 1: Confirm — no new scope-enforcement code needed (AC: #1, #3)
  - [x] `GET /api/production/machine-states` (Story 2.2) already resolves the caller's `CallerScope` server-side and filters via `IMachineRepository.ListByScopeAsync` across **every** Site/Line the JWT grants, not just one — this already covers "Manager with access to Line A and B sees only Line A/B machines" with zero extra production code. This task is verification, not implementation: read `MachineStatusQueryUseCase`/`ListByScopeAsync` (Story 2.2) and confirm they take the full `scope.SiteIds`/`scope.LineIds` sets, not a single id, before writing the AC #1 test
  - [x] AC #3 ("gọi API xin dữ liệu máy ngoài phạm vi... bị từ chối") has no spoofable target on this endpoint to reject in the first place — unlike the Story 1.2-1.5 master-data endpoints (which take an explicit `siteId`/`lineId` route parameter an attacker could swap), `GET /api/production/machine-states` takes **no** scope parameter at all; the result set is derived entirely from the caller's own JWT. There is nothing to "reject" because there is no out-of-scope request shape to make — the same "no single target to reject, filtering is the enforcement" reasoning Story 1.6 applied to `SiteManagementUseCase.ListAsync`. Prove this with a test (Task 3), don't add a redundant explicit-parameter+403 path that the endpoint's own shape doesn't need
- [x] Task 2: Frontend — empty state (AC: #2)
  - [x] `web/oee-shell/src/app/pages/dashboard/dashboard-page.ts` (Story 2.2/2.3): when the initial `listMachineStates()` load resolves with zero machines (not zero-because-still-loading — distinguish "loaded, empty" from "loading"), render an empty-state block instead of the card grid: icon + message + explicit "contact your Admin" guidance (UX-DR10 — "hiển thị hướng dẫn liên hệ Admin thay vì để trắng trơn", not a bare "no data" message)
  - [x] Add `dashboard.emptyState.title`/`dashboard.emptyState.message` keys to `public/i18n/en.json` and `public/i18n/vi.json`
- [x] Task 3: Testing (all AC)
  - [x] `tests/OeeNew.Api.Tests/Production/ProductionStatusEndpointsTests.cs` (Story 2.2/2.3, extend): mint a token scoped to two different Lines (`MasterDataApiFactory.CreateTokenFor("Manager", siteIds, lineIds)` — the overload already exists from Story 1.6) belonging to Machines seeded across both Lines plus a third Line the token does **not** grant; assert the response contains exactly the machines from the two granted Lines and none from the third — this is the AC #1 test. A second assertion in the same test class documents AC #3: request with a token scoped to Line C only, when Machines only exist under Lines A/B, returns an empty list (not an error, not another site's data) — proving there is no way to phrase this request to leak out-of-scope machines
  - [x] `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts` (extend): zero machines returned → empty-state block renders (not an empty grid, not a stuck skeleton)

## Dev Notes

- **This story is small on purpose.** Story 1.6 explicitly deferred exactly this AC pairing to here ("Story 2.4 (AC3)... sẽ tự viết lại đúng nguyên tắc khi các API đó tồn tại — không lặp lại kiểm thử ở đây") specifically so the scope-enforcement mechanism wouldn't be built twice. If this story is turning into new production backend code beyond what's listed in Task 1, stop and re-check — it's a sign `MachineStatusQueryUseCase` (Story 2.2) wasn't actually scope-general, which would be a Story 2.2 bug to fix there, not something to patch around here.
- **No dedicated multi-machine layout work.** Unlike Stories 2.2/2.3/2.5, the epics breakdown assigns **no UX-DR** to this story — the Machine Status Card grid Story 2.2 built already renders however many cards are in the result set; a Manager with 12 machines and an Operator with 1 hit the exact same component. Don't add Line-grouping headers, sorting controls, or other dashboard chrome not asked for by any AC here — that would be scope creep with no spec backing it.
- **Distinguish "still loading" from "loaded and empty" carefully in the FE.** Story 2.2's skeleton state (per-machine, `status: null`) is unrelated to this story's empty state (zero machines in the response entirely). Conflating them would either flash an empty-state message during normal load, or show a permanent skeleton for a genuinely unassigned user — check the array length *after* the initial fetch resolves, not before.

### Project Structure Notes

- No new folders. Changes land inside `pages/dashboard/` (Story 2.2) and the existing `tests/OeeNew.Api.Tests/Production/` test file (Story 2.2/2.3).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2] — Story 2.4 full AC (note: no UX-DR assigned to this story in the epic breakdown)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md#State-Patterns] — Empty state pattern (UX-DR10)
- [Source: _bmad-output/implementation-artifacts/1-6-site-line-selector-scope-enforcement.md] — the exact "filtering IS the enforcement, no single target to reject" precedent this story's AC #3 follows, and the explicit forward-reference to this story
- [Source: _bmad-output/implementation-artifacts/2-2-realtime-machine-status-dashboard.md] — `MachineStatusQueryUseCase`/`ListByScopeAsync`/`dashboard-page.ts` this story tests and extends

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia — BMad dev agent)

### Debug Log References

- Backend: `dotnet test` per project — Domain 59/59, Application 103/103, Architecture 2/2, Api 57/57 (221/221, all green; no regressions from Story 2.3's 219).
- `npx ng build` — succeeds.
- `npx tsc -p tsconfig.spec.json --noEmit` — clean. Per the same user instruction as Story 2.3, `ng test` was not re-run in this session; a follow-up run is recommended before merging.

### Completion Notes List

- Confirmed Task 1 by re-reading `MachineStatusQueryUseCase`/`MachineRepository.ListByScopeAsync` (Story 2.2): both operate on the full `scope.SiteIds`/`scope.LineIds` sets already, so no production scope-enforcement code was added here — only tests proving it (AC #1: two-Line Manager token; AC #3: token scoped to Lines that have no Machines returns an empty list, not an error or leaked data).
- Empty state (`loaded` signal) is deliberately separate from each card's own `status: null` skeleton (Story 2.2) — `loaded` only flips once the initial fetch resolves, regardless of how many machines came back.
- Added no dashboard chrome beyond the empty-state block (no Line grouping, no sorting) — the epics breakdown assigns no UX-DR to this story.

### File List

**Backend — modified:**
- `tests/OeeNew.Api.Tests/Production/ProductionStatusEndpointsTests.cs` (+2 tests for AC #1/#3; +2 helper methods)

**Frontend — modified:**
- `web/oee-shell/src/app/pages/dashboard/dashboard-page.ts` (`loaded` signal, empty-state template branch)
- `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts` (+1 empty-state test)
- `web/oee-shell/public/i18n/en.json`, `public/i18n/vi.json` (+ `dashboard.emptyState.*`)

## Change Log

- 2026-07-21: Confirmed Story 2.2's scope-filtered query already satisfies AC #1/#3 (no new backend code, tests added); added the Story 2.4 empty-state UI (UX-DR10) to the dashboard. Backend 221/221 tests passing; frontend type-checked clean, not executed via `ng test` in this session per user request. Status → review.
