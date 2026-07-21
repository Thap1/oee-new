---
baseline_commit: 153bffe33e1cfaa4730411a270388f54b0c1bd21
---

# Story 2.4: Dashboard tổng hợp nhiều máy/line theo phân quyền

Status: ready-for-dev

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

- [ ] Task 1: Confirm — no new scope-enforcement code needed (AC: #1, #3)
  - [ ] `GET /api/production/machine-states` (Story 2.2) already resolves the caller's `CallerScope` server-side and filters via `IMachineRepository.ListByScopeAsync` across **every** Site/Line the JWT grants, not just one — this already covers "Manager with access to Line A and B sees only Line A/B machines" with zero extra production code. This task is verification, not implementation: read `MachineStatusQueryUseCase`/`ListByScopeAsync` (Story 2.2) and confirm they take the full `scope.SiteIds`/`scope.LineIds` sets, not a single id, before writing the AC #1 test
  - [ ] AC #3 ("gọi API xin dữ liệu máy ngoài phạm vi... bị từ chối") has no spoofable target on this endpoint to reject in the first place — unlike the Story 1.2-1.5 master-data endpoints (which take an explicit `siteId`/`lineId` route parameter an attacker could swap), `GET /api/production/machine-states` takes **no** scope parameter at all; the result set is derived entirely from the caller's own JWT. There is nothing to "reject" because there is no out-of-scope request shape to make — the same "no single target to reject, filtering is the enforcement" reasoning Story 1.6 applied to `SiteManagementUseCase.ListAsync`. Prove this with a test (Task 3), don't add a redundant explicit-parameter+403 path that the endpoint's own shape doesn't need
- [ ] Task 2: Frontend — empty state (AC: #2)
  - [ ] `web/oee-shell/src/app/pages/dashboard/dashboard-page.ts` (Story 2.2/2.3): when the initial `listMachineStates()` load resolves with zero machines (not zero-because-still-loading — distinguish "loaded, empty" from "loading"), render an empty-state block instead of the card grid: icon + message + explicit "contact your Admin" guidance (UX-DR10 — "hiển thị hướng dẫn liên hệ Admin thay vì để trắng trơn", not a bare "no data" message)
  - [ ] Add `dashboard.emptyState.title`/`dashboard.emptyState.message` keys to `public/i18n/en.json` and `public/i18n/vi.json`
- [ ] Task 3: Testing (all AC)
  - [ ] `tests/OeeNew.Api.Tests/Production/ProductionStatusEndpointsTests.cs` (Story 2.2/2.3, extend): mint a token scoped to two different Lines (`MasterDataApiFactory.CreateTokenFor("Manager", siteIds, lineIds)` — the overload already exists from Story 1.6) belonging to Machines seeded across both Lines plus a third Line the token does **not** grant; assert the response contains exactly the machines from the two granted Lines and none from the third — this is the AC #1 test. A second assertion in the same test class documents AC #3: request with a token scoped to Line C only, when Machines only exist under Lines A/B, returns an empty list (not an error, not another site's data) — proving there is no way to phrase this request to leak out-of-scope machines
  - [ ] `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts` (extend): zero machines returned → empty-state block renders (not an empty grid, not a stuck skeleton)

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

### Debug Log References

### Completion Notes List

### File List
