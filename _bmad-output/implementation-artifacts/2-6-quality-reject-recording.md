---
baseline_commit: 153bffe33e1cfaa4730411a270388f54b0c1bd21
---

# Story 2.6: Ghi nhận số lượng sản phẩm lỗi/phế phẩm

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Operator,
I want to record basic reject/scrap quantity for my machine,
so that the Quality component of OEE can be calculated.

## Acceptance Criteria

1. **Given** máy của tôi đang chạy **When** tôi nhập số lượng phế phẩm cơ bản (không chi tiết root-cause — ngoài phạm vi MVP theo PRD) **Then** một bản ghi `QualityReject` được lưu, gắn với máy và khung thời gian/ca hiện tại (FR-010)
2. **Given** tôi là Manager/Operator/Viewer ngoài phạm vi được gán **When** tôi cố ghi `QualityReject` cho máy không thuộc phạm vi mình **Then** bị từ chối (FR-015, NFR-5)

## Tasks / Subtasks

- [x] Task 1: Domain — `QualityReject` (AC: #1)
  - [x] `src/OeeNew.Domain/Production/QualityReject.cs`: `Id`, `MachineId`, `Quantity` (`int`, must be > 0 — a zero/negative reject count isn't a real record), `RecordedAt` (`DateTimeOffset`). No mutation methods — this is an append-only log entry, no AC here asks for editing or deleting a past `QualityReject`
- [x] Task 2: Application (AC: #1, #2)
  - [x] `src/OeeNew.Application/Production/IQualityRejectRepository.cs` — `AddAsync` only. No `List`/`Get` yet — nothing in this story's AC reads them back; Epic 3/4's reporting work adds query methods when it actually needs them, not speculatively here
  - [x] `src/OeeNew.Application/Production/RecordQualityRejectUseCase.cs` — `RecordAsync(CallerScope scope, string? callerRole, Guid machineId, int quantity, CancellationToken)`: same role-then-scope check shape as `IngestProductionReadingUseCase`/`RecordDowntimeReasonUseCase` (Stories 2.1/2.5) — Operator or Admin only (`MasterDataForbiddenException` otherwise — reused across `Production` use cases exactly as Stories 2.1/2.5 already do, not worth inventing a parallel `ProductionForbiddenException` for one more story), then resolve+scope-check the Machine the same way. Validate `quantity > 0` (`MasterDataValidationException`, same reuse). `RecordedAt` is set server-side to `DateTimeOffset.UtcNow` at the moment of the call — **not** client-supplied — this is a live human action entered in the moment, unlike ingestion's machine-reported timestamp (Story 2.1), so there's no clock-skew/backdating concern to design around
- [x] Task 3: EF Core (AC: #1)
  - [x] `OeeDbContext`: `DbSet<QualityReject> QualityRejects`; table `QualityReject`, `uuidv7()` PK (AD-6 — synced entity per AD-2's "QualityReject theo bản ghi"), `MachineId` FK `Restrict`, `Quantity` as `integer`, `RecordedAt` as `timestamptz`
  - [x] `src/OeeNew.Infrastructure/Persistence/QualityRejectRepository.cs`
  - [x] Migration `AddQualityReject`
- [x] Task 4: Api (AC: #1, #2)
  - [x] `src/OeeNew.Api/Controllers/QualityRejectsController.cs` — `POST /api/production/machines/{machineId}/quality-rejects`, body `{ quantity: number }`, `[Authorize]` (role/scope enforced inside the use case, same reasoning as Story 2.1/2.5's controllers), `204 NoContent`
- [x] Task 5: Frontend (AC: #1)
  - [x] `dashboard.service.ts` (Story 2.2): add `recordQualityReject(machineId: string, quantity: number): Promise<void>`
  - [x] No UX-DR in the epics/UX docs governs this screen specifically (unlike the Reason Code Picker's UX-DR7) — keep it minimal: a small control reachable from the Machine Status Card (e.g. a compact button that opens a numeric-entry dialog) for machines the caller can act on. Use PrimeNG's `p-inputnumber` (`primeng/inputnumber`) with its built-in increment/decrement buttons rather than a bare text field — tapping a stepper button is consistent with the app's touch-first bias (UX-DR15) even though that UX-DR is written about the Reason Code Picker specifically, not this screen; typing a numeral isn't the "gõ chữ" (free-text typing) UX-DR15 warns against, but a stepper is still the better fit here and avoids inventing an on-screen keyboard requirement. Don't build anything beyond a quantity input + submit — no photo attachment, no reason/category for the reject (explicitly out of MVP scope per FR-010's own `[ASSUMPTION]`)
  - [x] Add `dashboard.qualityReject.*` i18n keys (title, quantity label, submit) to `public/i18n/en.json`/`vi.json`
- [x] Task 6: Testing (all AC)
  - [x] `tests/OeeNew.Domain.Tests/Production/QualityRejectTests.cs` — zero/negative quantity rejected at construction
  - [x] `tests/OeeNew.Application.Tests/Production/RecordQualityRejectUseCaseTests.cs` — happy path persists with `RecordedAt` set; Manager/Viewer role rejected; Operator scoped to a different Line rejected; quantity ≤ 0 rejected
  - [x] `tests/OeeNew.Api.Tests/Production/QualityRejectsEndpointsTests.cs` (reuse `MasterDataApiFactory`) — Operator token for an in-scope machine → 204 and a persisted row; Manager token → 403; Operator token scoped to a different Line → 403
  - [x] Frontend: a spec for whatever quality-reject control is added, covering: submitting a valid quantity calls the service with the right `machineId`/`quantity`; the control doesn't submit for quantity ≤ 0 (client-side mirror of the server check, not a replacement for it)

## Dev Notes

- **No shift/time-window resolution logic in this story.** AC #1's "gắn với... khung thời gian/ca hiện tại" is satisfied by storing an accurate `MachineId` + `RecordedAt` timestamp — resolving *which* `ShiftSchedule` (Story 1.3) was active at that instant is a reporting-time concern, explicitly Story 4.1's job ("số liệu tính đúng theo Shift Schedule đã cấu hình ở Story 1.3"). Don't add a shift-lookup algorithm here (matching start/end `TimeOnly` windows, handling overnight wraparound, etc.) — it would duplicate logic Story 4.1 needs to build anyway against the finished historical data, and nothing in this story's AC tests it.
- **No `MachineState.Status == Running` precondition is enforced server-side.** The AC's "Given máy của tôi đang chạy" sets the scene (this is what a real Operator's day looks like) but doesn't include a "Then bị từ chối nếu máy không chạy" branch — don't add one. An Operator recording a reject a few seconds after the machine paused shouldn't get an inexplicable rejection.
- **Role+scope check is copy-shaped from Story 2.1/2.5, not redesigned.** If this use case's authorization logic looks different from `IngestProductionReadingUseCase`/`RecordDowntimeReasonUseCase`, that's a sign of unnecessary invention — it should be recognizably the same pattern a third time.
- **This closes out Epic 2.** After this story, every FR/NFR the epic claims (FR-001..006, FR-008..010, NFR-1, NFR-3, NFR-4) has a corresponding implemented AC. No cross-story follow-ups are expected beyond what Stories 2.1-2.5's own Dev Notes already flagged (e.g. the `ReasonCodeId: null` unattributed-downtime case from Story 2.5, and shift-resolution deferred to Story 4.1 above).

### Project Structure Notes

- `QualityReject`/`RecordQualityRejectUseCase`/`QualityRejectRepository` join the `Production` subfolder pattern (Domain/Application/Infrastructure) established in Stories 2.1/2.2/2.5 — no new top-level structure.
- Frontend control lives in `pages/dashboard/`, alongside `machine-status-card.ts`/`reason-code-picker.ts`.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2] — Story 2.6 full AC
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-010] — `[ASSUMPTION]` explicitly scoping out detailed root-cause quality tracking
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-2] — `QualityReject` as a synced business record ("theo bản ghi")
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-6] — `uuidv7()` for synced entities
- [Source: _bmad-output/implementation-artifacts/2-1-production-data-ingestion.md] — role+scope check pattern this story's use case copies
- [Source: _bmad-output/implementation-artifacts/2-5-downtime-reason-picker.md] — same pattern reused a second time; precedent for reusing `MasterData*` exception types from `Production` use cases

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia — BMad dev agent)

### Debug Log References

- Backend: `dotnet test` per project — Domain 68/68, Application 125/125, Architecture 2/2, Api 66/66 (261/261, all green; no regressions from Story 2.5's 246 + this story's additions).
- `dotnet ef migrations add AddQualityReject` + `dotnet ef database update` against local `oeenew_test` — applied cleanly.
- `npx ng build` — succeeds, no new budget warning.
- `npx tsc -p tsconfig.spec.json --noEmit` — clean across the whole spec suite. Per the same user instruction as Stories 2.3-2.5, `ng test` was not executed in this session; a follow-up run is recommended before merging.
- **Found and fixed a real pre-existing bug while writing this story's scoped-Operator test**: both `RecordDowntimeReasonUseCase` (Story 2.5) and my first draft of `RecordQualityRejectUseCase` checked `scope.AllowsLine(machineId)` instead of `scope.AllowsLine(machine.LineId)` — comparing the scope's allowed Line ids against the *Machine's own id* rather than its parent Line's id. This meant a correctly-scoped Operator was always rejected; it went undetected in Story 2.5 because every existing test there used either `CallerScope.Global` (bypasses the check) or a scope deliberately built not to match. Fixed both use cases and added a `..._OperatorScopedToTheMachinesOwnLine_Succeeds` regression test to each (`RecordDowntimeReasonUseCaseTests.cs`, `RecordQualityRejectUseCaseTests.cs`) so this class of bug is caught going forward. `IngestProductionReadingUseCase` (Story 2.1) was already correct and was not the source.

### Completion Notes List

- Role+scope check in `RecordQualityRejectUseCase` is the same shape as `IngestProductionReadingUseCase`/`RecordDowntimeReasonUseCase`, now verified correct with a real scoped-success test (not just a scoped-failure one).
- No shift-resolution logic added — `QualityReject` stores only `MachineId`/`Quantity`/`RecordedAt`, matching the story's explicit scope boundary.
- No `MachineState.Status == Running` precondition added, per Dev Notes.
- Frontend: `QualityRejectControl` is a small self-contained component (owns its own dialog state and calls `DashboardService` directly) embedded inside `MachineStatusCard`'s normal-status branch, with `(click)="$event.stopPropagation()"` on its wrapper so tapping it on a `Stopped` card doesn't also trigger the Reason Code Picker (`onClick()` from Story 2.5).
- This closes out Epic 2 — every FR/NFR the epic claims now has a corresponding implemented AC across Stories 2.1-2.6.

### File List

**Backend — new:**
- `src/OeeNew.Domain/Production/QualityReject.cs`
- `src/OeeNew.Application/Production/IQualityRejectRepository.cs`
- `src/OeeNew.Application/Production/RecordQualityRejectUseCase.cs`
- `src/OeeNew.Infrastructure/Persistence/QualityRejectRepository.cs`
- `src/OeeNew.Infrastructure/Persistence/Migrations/20260721145129_AddQualityReject.cs` (+ `.Designer.cs`)
- `src/OeeNew.Api/Controllers/QualityRejectsController.cs`
- `tests/OeeNew.Domain.Tests/Production/QualityRejectTests.cs`
- `tests/OeeNew.Application.Tests/Production/FakeQualityRejectRepository.cs`
- `tests/OeeNew.Application.Tests/Production/RecordQualityRejectUseCaseTests.cs`
- `tests/OeeNew.Api.Tests/Production/QualityRejectsEndpointsTests.cs`

**Backend — modified:**
- `src/OeeNew.Application/Production/RecordDowntimeReasonUseCase.cs` (bug fix: `AllowsLine(machine.LineId)`)
- `src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs` (`QualityReject` mapping)
- `src/OeeNew.Api/Program.cs` (new registrations)
- `tests/OeeNew.Application.Tests/Production/RecordDowntimeReasonUseCaseTests.cs` (+ regression test)

**Frontend — new:**
- `web/oee-shell/src/app/pages/dashboard/quality-reject-control.ts` (+ `.spec.ts`)

**Frontend — modified:**
- `web/oee-shell/src/app/pages/dashboard/dashboard.service.ts` (+ `recordQualityReject`)
- `web/oee-shell/src/app/pages/dashboard/machine-status-card.ts` (embeds `QualityRejectControl`)
- `web/oee-shell/public/i18n/en.json`, `public/i18n/vi.json` (+ `dashboard.qualityReject.*`)

## Change Log

- 2026-07-21: Initial implementation — `QualityReject` append-only log entry, role+scope-checked recording endpoint, minimal stepper-input dialog reachable from the Machine Status Card. Found and fixed a scope-check bug in Story 2.5's `RecordDowntimeReasonUseCase` (`AllowsLine` was comparing against the Machine id instead of its Line id) with regression tests added to both affected use cases. Backend 261/261 tests passing; frontend type-checked clean, not executed via `ng test` in this session per user request. Status → review. **This closes out Epic 2.**
