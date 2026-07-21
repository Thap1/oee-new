---
baseline_commit: 153bffe33e1cfaa4730411a270388f54b0c1bd21
---

# Story 2.5: Ghi nhận lý do dừng máy (Reason Code Picker)

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Operator,
I want to select a downtime reason from a large grouped button grid when my machine stops,
so that I can record it in a few seconds without typing or leaving my position.

## Acceptance Criteria

1. **Given** card máy của tôi đang `Stopped` **When** tôi chạm vào card **Then** Reason Code Picker mở full-screen, hiện các Reason Code đang active nhóm theo `LossCategory` (Availability/Performance/Quality) (FR-008, FR-009, UX-DR7)
2. **Given** picker đang mở **When** tôi chạm 1 lần vào một nút lý do **Then** `DowntimeEvent` được ghi nhận với ReasonCode đó, picker tự đóng, không có bước xác nhận phụ (UX-DR7)
3. **Given** một `DowntimeEvent` đang mở (máy còn dừng) và máy chạy lại **When** trạng thái Running được nhận **Then** `DowntimeEvent` tự đóng kèm timestamp kết thúc (khớp bản ghi nghiệp vụ đã chốt theo AD-2)
4. **Given** các nút lý do được hiển thị **When** render **Then** mỗi nút có `minTouchTarget` ≥64px (UX-DR7)
5. **Given** một Reason Code đã có ít nhất 1 `DowntimeEvent` tham chiếu **When** Admin cố xoá cứng Reason Code đó (Epic 1, Story 1.5) **Then** bị chặn để bảo toàn báo cáo lịch sử — Admin chỉ có thể deactivate, không xoá được (bổ sung cho Story 1.5 AC2, chỉ testable từ đây trở đi vì `DowntimeEvent` mới tồn tại)

## Tasks / Subtasks

- [ ] Task 1: Domain — `DowntimeEvent` (AC: #2, #3)
  - [ ] `src/OeeNew.Domain/Production/DowntimeEvent.cs`: `Id`, `MachineId`, `ReasonCodeId` (`Guid?` — starts unassigned), `StartedAt`, `EndedAt` (`DateTimeOffset?`), `IsOpen => EndedAt is null`. Ctor `(Guid id, Guid machineId, DateTimeOffset startedAt)`. `AssignReason(Guid reasonCodeId)` — overwrites freely while `IsOpen` (an Operator tapping the wrong button, then tapping the card again, must be able to correct it — no "already assigned" guard while open); throws `DowntimeEventNotOpenException` if called on a closed event. `Close(DateTimeOffset endedAt)` — sets `EndedAt`; no-op guard against double-close (defensive; shouldn't happen given `MachineState`'s stale-reading guard from Story 2.1, but a closed event silently ignoring a second close is safer than throwing on an ingestion path)
  - [ ] `src/OeeNew.Domain/Production/DowntimeEventNotOpenException.cs`
- [ ] Task 2: Application — downtime lifecycle is ingestion-driven, reason-assignment is Operator-driven; these are two different use cases (AC: #1, #2, #3)
  - [ ] `src/OeeNew.Application/Production/IDowntimeEventRepository.cs`: `AddAsync`, `GetOpenByMachineIdAsync(machineId)`, `UpdateAsync`, `ExistsForReasonCodeAsync(reasonCodeId)` (the last one is what Task 5 needs)
  - [ ] **Modify `IngestProductionReadingUseCase`** (Story 2.1, extended by Story 2.2) again: capture the Machine's status **before** calling `MachineState.Apply`. After a successful (non-stale) apply: if the status transitioned *into* `Stopped` from anything else, open a new `DowntimeEvent(Guid.Empty, machineId, reading.Timestamp)` via `AddAsync` — `StartedAt` is the actual machine-stop timestamp reported by the machine, **not** whenever the Operator later gets around to tapping a reason (this is why the event opens automatically on ingestion instead of when the Picker submits — an accurate Availability-loss duration depends on it). If the status transitioned *out of* `Stopped` (i.e. to `Running`/`Idle`/`Fault`), look up the open `DowntimeEvent` for that machine and `Close(reading.Timestamp)` it (AC #3) — if none is open, there's nothing to close, don't throw (e.g. the event was seeded before this story shipped, or a status oscillated faster than expected)
  - [ ] An event can close with `ReasonCodeId` still `null` if the Operator never tapped a reason before the machine resumed — this is an accepted MVP edge case (an "unattributed" downtime), not a bug to prevent here; flag it in Dev Notes below rather than building retroactive-assignment UI for it
  - [ ] `src/OeeNew.Application/Production/RecordDowntimeReasonUseCase.cs` — `AttachReasonAsync(CallerScope scope, string? callerRole, Guid machineId, Guid reasonCodeId, CancellationToken)`: role check (Operator/Admin, same as `IngestProductionReadingUseCase`'s check from Story 2.1); resolve+scope-check the Machine (same pattern); resolve the ReasonCode, reject if missing, if `!IsActive`, or if it belongs to a different Site than the Machine's (reuse `MasterDataValidationException`/`MasterDataParentNotFoundException` as fits — don't invent new exception types for cases the existing ones already describe); `GetOpenByMachineIdAsync` — if `null`, throw `DowntimeEventNotOpenException` (maps to 404 — the machine isn't currently down, there's nothing to attach a reason to; the picker should only ever be open when the card is `Stopped`, but the server never trusts that); otherwise `AssignReason` + `UpdateAsync` + notify (next bullet)
  - [ ] Extend `IMachineStatusNotifier` (Story 2.2) with `Task NotifyDowntimeReasonRecordedAsync(Guid machineId, Guid reasonCodeId, CancellationToken)`, broadcast as SignalR event `DowntimeReasonRecorded` — this exact event name is already named in the Architecture Spine's Consistency Conventions table (`SignalR | ... tên event dạng MachineStatusChanged, DowntimeReasonRecorded`), so emitting it is completing an already-specified contract, not new scope. No FE code in this story needs to **subscribe** to it (nothing on the current dashboard visually changes when a reason is attached — the card's `status` doesn't change) — emit it and stop there
- [ ] Task 3: Backend — expose `SiteId` on the machine-status projection (AC: #1)
  - [ ] `MachineStatusSnapshot`/`MachineStatusQueryUseCase` (Story 2.2) and the `GET /api/production/machine-states` response (Story 2.2/2.3): add `SiteId` (resolved via the Machine's Line, same join `ListByScopeAsync` already performs) — the Picker needs the machine's Site to fetch that Site's Reason Codes, and re-deriving it client-side from `ScopeService` would require the FE to already have every Line→Site mapping cached, which it doesn't. Cheaper to include the one extra field server-side than to build that cache
- [ ] Task 4: EF Core — persistence (AC: #2, #3)
  - [ ] `OeeDbContext`: `DbSet<DowntimeEvent> DowntimeEvents`; table `DowntimeEvent`, `uuidv7()` PK (AD-6 — this is a synced entity per AD-2), `MachineId`/`ReasonCodeId` as `uuid` (`ReasonCodeId` nullable), `HasOne<Machine>().WithMany().OnDelete(DeleteBehavior.Restrict)`, `HasOne<ReasonCode>().WithMany().OnDelete(DeleteBehavior.Restrict)` (nullable FK), `StartedAt`/`EndedAt` as `timestamptz`
  - [ ] `src/OeeNew.Infrastructure/Persistence/DowntimeEventRepository.cs`
  - [ ] Migration `AddDowntimeEvent`
- [ ] Task 5: Backend — Story 1.5's missing hard-delete, now that something can depend on a Reason Code (AC: #5)
  - [ ] `IReasonCodeRepository`: add `DeleteAsync(ReasonCode reasonCode, CancellationToken)` (Story 1.5 only ever needed `AddAsync`/`GetAsync`/`ListBySiteAsync`/`UpdateAsync` — deactivate-only — so this method has never existed until now)
  - [ ] `ReasonCodeManagementUseCase.DeleteAsync(string? callerRole, Guid id, CancellationToken)`: `EnsureAdmin`; resolve reason code (`MasterDataNotFoundException` if missing); `downtimeEvents.ExistsForReasonCodeAsync(id)` → if true, throw the **existing** `MasterDataHasDependentsException("ReasonCode", id, ["existing downtime records"])` — reuse the exact exception Site/Line deletion already uses (already mapped to 409 `HAS_DEPENDENTS` in `ApiExceptionHandler`; the FE's existing `masterData.error.hasDependents` i18n key already renders `dependentNames` generically, no FE change needed for the error path), otherwise delete
  - [ ] `ReasonCodesController`: `DELETE /api/master-data/reason-codes/{id}`, `[Authorize(Policy = "AdminOnly")]`, `NoContent()` — the one genuinely new master-data endpoint in this story
  - [ ] `web/oee-shell/src/app/pages/master-data/master-data.service.ts`: add `deleteReasonCode(id): Promise<void>`; wire a delete action into the existing Reason Codes admin table (`master-data-page.html`) the same way Site/Line/Machine deletes already work there
- [ ] Task 6: Frontend — Reason Code Picker (AC: #1, #2, #4)
  - [ ] `web/oee-shell/src/app/pages/dashboard/reason-code-picker.ts` — full-screen overlay, `@Input() open`, `@Input() reasonCodes: ReasonCodeDto[]` (already-fetched, active-only — filtering below), `@Output() reasonSelected: EventEmitter<string>` (reason code id), `@Output() closed: EventEmitter<void>`. Groups the input list by `lossCategory` into three sections (Availability/Performance/Quality) — **reuse the existing `masterData.lossCategory.*` i18n keys from Story 1.5** for the section headers, don't add duplicate dashboard-scoped translation keys for the same three labels. Each reason button: single `(click)` handler emits `reasonSelected` immediately (no confirm dialog, no second tap) and the parent closes the picker on success. `min-height: 64px; min-width: 64px` per `reason-code-button` token (`DESIGN.md`) — AC #4
  - [ ] `dashboard.service.ts` (Story 2.2): add `recordDowntimeReason(machineId: string, reasonCodeId: string): Promise<void>` → `POST /api/production/machines/{machineId}/downtime-reason`
  - [ ] `machine-status-card.ts` (Story 2.2): add `@Output() cardTapped = new EventEmitter<string>()`, wired to a click handler that only emits when `snapshot().status === 'Stopped'` (tapping a running/idle/no-signal card does nothing — matches UX-DR7 literally: the Picker opens "khi máy đang Stopped", not on every tap)
  - [ ] `dashboard-page.ts`: on `cardTapped`, fetch that machine's Site's active Reason Codes (`masterDataService.listReasonCodes(siteId)` from Story 1.5 — already exists, filter to `isActive` client-side here since the admin list endpoint intentionally returns both active and inactive for the Master Data screen's own use) and open the picker; on `reasonSelected`, call `recordDowntimeReason` then close the picker regardless of the specific error (a 404 "not open anymore" is a legitimate race — Task 2 — show a brief message, not a crash); on `closed`, just hide it
- [ ] Task 7: Testing (all AC)
  - [ ] `tests/OeeNew.Domain.Tests/Production/DowntimeEventTests.cs` — open→assign→close lifecycle; re-assign while open overwrites; assign after close throws; double-close is a no-op
  - [ ] Extend `tests/OeeNew.Application.Tests/Production/IngestProductionReadingUseCaseTests.cs` — transition into `Stopped` opens a `DowntimeEvent` with `StartedAt` matching the reading's timestamp; transition out of `Stopped` closes the open event with the correct `EndedAt`; a non-`Stopped`-involving transition (e.g. `Running`→`Idle`) touches no `DowntimeEvent`
  - [ ] `tests/OeeNew.Application.Tests/Production/RecordDowntimeReasonUseCaseTests.cs` — happy path assigns + notifies; inactive reason code rejected; reason code from a different Site rejected; no open `DowntimeEvent` → `DowntimeEventNotOpenException`; Manager/Viewer role rejected; Operator scoped to a different Line rejected
  - [ ] Extend `tests/OeeNew.Application.Tests/MasterData/ReasonCodeManagementUseCaseTests.cs` — delete blocked when a `DowntimeEvent` references the reason code (409-mapping exception thrown); delete succeeds when none do
  - [ ] `tests/OeeNew.Api.Tests/Production/DowntimeReasonEndpointsTests.cs` — full flow: ingest a reading that stops a machine → attach a reason via the endpoint → 204; attach on a machine that's already running again → 404
  - [ ] Extend `tests/OeeNew.Api.Tests/MasterData/ReasonCodesEndpointsTests.cs` — `DELETE` blocked (409) once a `DowntimeEvent` references it; allowed (204) otherwise; non-Admin caller → 403
  - [ ] `web/oee-shell/src/app/pages/dashboard/reason-code-picker.spec.ts` — groups correctly by loss category; each button ≥64px (check computed style or a CSS class assertion); single click emits `reasonSelected` and nothing else (no intermediate confirm state)
  - [ ] Extend `web/oee-shell/src/app/pages/dashboard/machine-status-card.spec.ts` — click on a `Stopped` card emits `cardTapped`; click on any other status emits nothing
  - [ ] Extend `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts` — tapping a stopped card opens the picker with that machine's active-only reason codes; selecting one calls the service and closes the picker

## Dev Notes

- **The DowntimeEvent lifecycle belongs to ingestion, not to the Picker.** This is the single most important design decision in this story: `StartedAt`/`EndedAt` are set purely from machine status transitions (Story 2.1's ingestion path, extended here), because that's the only way the recorded downtime duration reflects when the machine actually stopped/resumed rather than how quickly an Operator happened to tap a button. The Picker's job (`RecordDowntimeReasonUseCase`) is narrower than it looks: it only ever **attaches a category to an already-open event**, it never opens or closes one. If an implementation has the Picker's endpoint creating the `DowntimeEvent` row, that's the wrong shape — go back and wire it into `IngestProductionReadingUseCase` instead.
- **An event closing with `ReasonCodeId: null` is expected, not a defect.** Don't add validation forcing a reason before close, and don't build a "assign a reason retroactively to a past downtime" flow — nothing in the ACs asks for it, and Epic 3/4's reporting can treat a null reason as an "Unattributed" bucket if/when that becomes a visible problem. This is exactly the kind of scenario the project's "don't build for hypotheticals" guidance means to prevent.
- **`DowntimeReasonRecorded` is emitted, not consumed, in this story.** It's named explicitly in the Architecture Spine's Consistency Conventions table alongside `MachineStatusChanged`, so wiring the broadcast is completing a documented contract — but no current screen needs to react to it (a `Stopped` card doesn't change appearance when its reason gets tagged). Don't build FE subscription logic for it; a later story can add that if a real need shows up (e.g. a Manager wanting to see "reason pending" vs "reason recorded" on their own dashboard — not asked for here).
- **Reuse `masterData.lossCategory.*` i18n keys for the Picker's group headers** (`AvailabilityLoss`/`PerformanceLoss`/`QualityLoss` — already defined in Story 1.5's `public/i18n/{en,vi}.json`). Don't add a second, dashboard-scoped copy of the same three labels.
- **Task 5 (hard-delete) exists because Story 1.5 never built one, not because this story's own AC needs a brand-new master-data endpoint by itself.** Re-read `ReasonCodeManagementUseCase`/`ReasonCodesController` before starting — there is currently no `DeleteAsync`/`DELETE` route at all, only `Deactivate`. AC #5's "Admin cố xoá cứng... bị chặn" only becomes a real, testable scenario once that endpoint exists, which is why adding it (guarded by the dependents-check) is in scope here rather than being pure Story-1.5 leftover work.
- **`RecordDowntimeReasonUseCase`'s role/scope check duplicates `IngestProductionReadingUseCase`'s shape exactly (Story 2.1)** — same Operator-or-Admin check, same "resolve Machine, resolve its Line, check `CallerScope.AllowsSite`/`AllowsLine`" pattern. Don't design a different check here; copy the established one.
- **`SiteId` on the machine-status response (Task 3) is additive** — existing FE code reading `MachineStatusDto` (Stories 2.2-2.4) doesn't break, it just gains a field it can now use.

### Project Structure Notes

- `Production` subfolder (Domain/Application/Infrastructure) gains its second and third entities/use cases (`DowntimeEvent`, `RecordDowntimeReasonUseCase`) alongside Story 2.1/2.2's `MachineState`/ingestion code — same folder, no new top-level structure.
- `reason-code-picker.ts` lives in `pages/dashboard/` next to `machine-status-card.ts` — it's a dashboard-triggered overlay, not a standalone routed page.
- The one MasterData-layer change (Task 5) touches files Story 1.5 already created (`ReasonCodeManagementUseCase.cs`, `ReasonCodesController.cs`, `IReasonCodeRepository.cs`, `ReasonCodeRepository.cs`, `master-data.service.ts`, `master-data-page.html`) — extend them in place, don't fork a parallel "v2" of any of them.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2] — Story 2.5 full AC
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-2] — "DowntimeEvent khi đã đóng" as the synced business record; `StartedAt`/`EndedAt` semantics
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-6] — `uuidv7()` for `DowntimeEvent` (it's a synced entity)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Consistency-Conventions] — `DowntimeReasonRecorded` event name
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/DESIGN.md#components] — `reason-code-button` token (`minTouchTarget: 64px`)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md#Key-Flows] — UJ-1, the full Operator downtime-recording flow this story implements
- [Source: _bmad-output/implementation-artifacts/1-5-reason-code-management.md] — `ReasonCodeManagementUseCase`/`ReasonCodesController` this story extends; the forward-reference to this story for Picker active-filtering and hard-delete
- [Source: _bmad-output/implementation-artifacts/2-1-production-data-ingestion.md], [2-2-realtime-machine-status-dashboard.md] — `IngestProductionReadingUseCase`, `IMachineStatusNotifier`, `MachineStatusSnapshot` this story extends

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
