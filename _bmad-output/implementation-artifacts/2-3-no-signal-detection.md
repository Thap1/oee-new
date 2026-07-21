---
baseline_commit: 153bffe33e1cfaa4730411a270388f54b0c1bd21
---

# Story 2.3: Phát hiện & hiển thị mất tín hiệu máy

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Operator/Admin,
I want a machine that stops sending data for too long to show a distinct "no signal" state,
so that I don't mistake a connectivity problem for a real stoppage.

## Acceptance Criteria

1. **Given** một máy không gửi dữ liệu quá thời gian cấu hình **When** vượt ngưỡng **Then** card chuyển `status-no-signal` (xám) + icon mất kết nối + nhãn "Mất tín hiệu Xp" (FR-003, UX-DR9)
2. **Given** máy đang ở trạng thái no-signal **When** so với `status-stopped` (đỏ) **Then** tách biệt rõ về màu sắc/ngữ nghĩa — no-signal không bao giờ được tính là DowntimeEvent thật
3. **Given** máy gửi dữ liệu trở lại **When** dữ liệu mới đến **Then** card quay về đúng trạng thái thật (Running/Stopped/Idle) theo báo cáo mới nhất

## Tasks / Subtasks

- [ ] Task 1: Backend — single source of truth for the threshold (AC: #1)
  - [ ] `appsettings.json` (`src/OeeNew.Api/`): add `"Production": { "NoSignalThresholdSeconds": 60 }` — `[ASSUMPTION]` 60s is a placeholder default (no real machine cadence is known yet, same caveat the PRD itself flags for other MVP guesses); it must be a config value, not a hardcoded magic number, precisely so it can be tuned later without a code change
  - [ ] `src/OeeNew.Infrastructure/Production/ProductionOptions.cs` (or similar `Options` class bound to the `Production` section) exposing `NoSignalThresholdSeconds`
  - [ ] Extend the `GET /api/production/machine-states` response from Story 2.2 (`src/OeeNew.Api/Controllers/ProductionStatusController.cs`) to a small wrapper: `{ noSignalThresholdSeconds: number, machines: MachineStatusResponse[] }` instead of a bare array — one round trip gives the dashboard everything it needs to compute staleness itself. This is a deliberate change to Story 2.2's endpoint shape; update its FE caller (`dashboard.service.ts`) in the same story, don't leave two shapes disagreeing
- [ ] Task 2: Frontend — no-signal is a computed display override, not new backend state (AC: #1, #2, #3)
  - [ ] `web/oee-shell/src/app/core/realtime/clock-tick.service.ts` — tiny signal-based service exposing a `nowMs` signal refreshed every ~10s via `setInterval` (injected `DestroyRef` to clear it). One shared ticking clock, not one `setInterval` per card
  - [ ] `web/oee-shell/src/app/pages/dashboard/machine-status-card.ts` (Story 2.2): add a `computed()` combining `snapshot().lastReportedAt`, the injected threshold (passed down from `dashboard-page.ts`, which read it from Task 1's response), and `clockTick.nowMs()`. When `lastReportedAt` is set **and** `(now - lastReportedAt) > thresholdSeconds`, render the `status-no-signal` gray variant (icon: a "disconnected"/`pi-wifi`-off-style icon, distinct from every `MachineStatus` icon Story 2.2 defined) with label `dashboard.status.noSignal` ("Mất tín hiệu {{minutes}}p" / "No signal {{minutes}}m") — this **overrides** whatever `snapshot().status` says (AC #2: a machine last reported as `Stopped` 10 minutes ago must render gray no-signal, not red stopped, once past threshold). When `lastReportedAt` is `null` (never reported — Story 2.2's skeleton case), keep rendering the skeleton, not a no-signal card — "never connected" and "was connected, now silent" are different states and only the latter is this story's concern
  - [ ] No special "recovery" code needed for AC #3: once a new `MachineStatusChanged` event updates `snapshot().lastReportedAt` (Story 2.2's existing SignalR handling), the `computed()` in the previous bullet re-evaluates on its own and the override clears — confirm this with a test (Task 4) rather than adding new logic
  - [ ] Add `dashboard.status.noSignal` (and any other status-label keys Story 2.2 left implicit) to both `public/i18n/en.json` and `public/i18n/vi.json`
- [ ] Task 3: Guardrail note for Story 2.5 (no code in this story)
  - [ ] `DowntimeEvent` doesn't exist yet (Story 2.5 introduces it) — there is nothing to implement here for AC #2's "no-signal không bao giờ được tính là DowntimeEvent thật." This task exists only to make sure the constraint is written down before 2.5 starts: whatever opens/closes a `DowntimeEvent` in Story 2.5 must key off the machine's actual reported `Status` transitions (`Running` ↔ `Stopped`), never off a no-signal timeout. Silence (no new readings) must never itself fabricate a `DowntimeEvent` — flag this explicitly in Story 2.5's own Dev Notes when that story is created, don't rely on this story's Dev Notes surviving into that context unread
- [ ] Task 4: Testing (all AC)
  - [ ] `tests/OeeNew.Api.Tests/Production/ProductionStatusEndpointsTests.cs` (Story 2.2, extend) — response now includes `noSignalThresholdSeconds` matching configuration
  - [ ] `web/oee-shell/src/app/pages/dashboard/machine-status-card.spec.ts` (Story 2.2, extend): `lastReportedAt` within threshold + `status: Stopped` → red stopped card (not gray); `lastReportedAt` older than threshold + `status: Stopped` → gray no-signal card with elapsed-minutes label, and this must visibly differ (different CSS class/color) from the stopped-card case in the same test file so a regression that makes them look identical is caught; `lastReportedAt: null` → still skeleton, not gray no-signal
  - [ ] `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts` (Story 2.2, extend): simulate time passing past the threshold (fake the clock-tick signal or inject a controllable clock) on a card that had real data → card flips to no-signal without any new SignalR event; then simulate a `MachineStatusChanged` event arriving → card returns to its real status (AC #3)

## Dev Notes

- **This is a presentation-layer feature, not a new backend detection system — the PRD says so explicitly.** FR-003: "Hệ thống phải phát hiện và cảnh báo (**ở mức hiển thị**) khi một máy ngừng gửi dữ liệu quá thời gian quy định." No backend job, timer, or scheduled task computes staleness; the dashboard computes `now - lastReportedAt > threshold` reactively, using the same `MachineStatusDto`/SignalR data Story 2.2 already delivers. Don't build a background service, a `NoSignalDetectedEvent`, or any new persisted state — there is nothing to persist here, "no signal" is true or false purely as a function of the current wall-clock time and the last known `lastReportedAt`.
- **Why the threshold rides along in the `GET /api/production/machine-states` response instead of a separate endpoint or an FE-hardcoded constant:** the dashboard already calls this endpoint once at load (Story 2.2); piggybacking the threshold avoids a second round trip and keeps the number's only source of truth on the backend (`appsettings.json`), where an operator/admin can eventually tune it without touching FE code.
- **No-signal always wins over the last known status once past threshold (AC #2).** This is the one piece of real logic in the story: the card's rendering must check staleness *before* branching on `status`, not the other way around. A wrong implementation order (check `status` first, only consider staleness for some branches) would let a stale `Stopped` reading masquerade as a real stoppage — exactly the confusion UX-DR9/FR-003 exist to prevent.
- **Don't touch `MachineState.Apply` or the ingestion path (Story 2.1/2.2) at all.** No-signal detection reads `lastReportedAt`, it never writes it, and it has no opinion about cumulative counters or status enums. If you find yourself editing `IngestProductionReadingUseCase` for this story, stop — that's a sign the design has drifted from "presentation-layer" into "backend state machine," which is not what FR-003 asks for.
- **The 60s default is a placeholder, flag it as such if the dev agent or a reviewer questions it** — same spirit as the PRD's own `[ASSUMPTION]` markers elsewhere. It is a config value specifically so it doesn't need to be "right" on the first guess.

### Project Structure Notes

- No new top-level folders. `clock-tick.service.ts` joins the `core/realtime/` folder Story 2.2 created (shared plumbing, same reasoning as `ScopeService`/`AuthService` living in `core/`, not a page folder).
- `ProductionOptions.cs` goes in `src/OeeNew.Infrastructure/Production/` (new small folder, config binding is an Infrastructure concern — compare to how `JwtOptions`/`BootstrapAdminOptions` live in `Infrastructure/Identity/`, not `Application`).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2] — Story 2.3 full AC
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-003] — "ở mức hiển thị" wording that drives the presentation-layer-only design
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/DESIGN.md#colors] — `status-no-signal` `#6B7280`, already wired into `styles.scss` by Story 2.2 Task 3
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md#State-Patterns] — "No signal" card pattern, distinct from `status-stopped`
- [Source: _bmad-output/implementation-artifacts/2-2-realtime-machine-status-dashboard.md] — `MachineStatusDto`, `machine-status-card.ts`, `dashboard-page.ts`, SignalR wiring this story extends; the skeleton-vs-no-signal distinction this story preserves

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
