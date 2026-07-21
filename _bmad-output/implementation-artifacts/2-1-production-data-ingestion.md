---
baseline_commit: 153bffe33e1cfaa4730411a270388f54b0c1bd21
---

# Story 2.1: Nhận & chuẩn hoá dữ liệu sản xuất từ máy (Ingestion)

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Operator/system integrator,
I want the system to accept machine production/status data through a single standardized endpoint,
so that OEE calculation logic never changes when a new machine type or protocol is added.

## Acceptance Criteria

1. **Given** máy gửi dữ liệu tới ingestion endpoint với các field `machine_id`/`timestamp`/`counter`/`status` **When** hệ thống nhận **Then** dữ liệu được xử lý qua interface `IProductionDataSource`, Domain/Application không biết chi tiết giao thức (AD-3)
2. **Given** giá trị counter **When** xử lý **Then** được hiểu là giá trị luỹ kế (cumulative), không phải delta (AD-3)
3. **Given** giá trị status **When** xử lý **Then** phải là một trong enum `Running|Stopped|Idle|Fault`; giá trị khác bị từ chối theo error envelope chuẩn
4. **Given** một máy chưa kết nối tự động được **When** Operator/Admin nhập bù sản lượng/trạng thái thủ công **Then** dữ liệu đi qua đúng luồng domain giống hệt ingestion tự động (FR-002)
5. **Given** trung tâm (Central) không kết nối được **When** Operator ghi nhận downtime/dashboard cập nhật tại site **Then** mọi thao tác vẫn hoạt động đầy đủ (NFR-3, NFR-4) — site không phụ thuộc trung tâm cho vận hành hàng ngày

## Tasks / Subtasks

- [ ] Task 1: Domain layer — normalized reading contract + current-state entity (AC: #1, #2, #3)
  - [ ] `src/OeeNew.Domain/Production/MachineStatus.cs` — enum `Running | Stopped | Idle | Fault` (AD-3's fixed enum, not a free string)
  - [ ] `src/OeeNew.Domain/Production/IProductionDataSource.cs` — interface with `Guid MachineId`, `DateTimeOffset Timestamp`, `long Counter`, `MachineStatus Status`. This is the ONLY shape Domain/Application ever see; any adapter (real protocol, manual entry, simulated script) implements or maps into it in Infrastructure/Api, never the reverse
  - [ ] `src/OeeNew.Domain/Production/MachineState.cs` — one row per Machine holding its latest known reading: `MachineId` (PK, matches `Machine.Id` 1:1), `Status`, `Counter`, `LastReportedAt`. Constructor for first-ever reading; an `Apply(MachineStatus status, long counter, DateTimeOffset reportedAt)` method for subsequent readings that **ignores an incoming reading whose `reportedAt` is older than the currently stored `LastReportedAt`** (out-of-order network delivery guard — return a `bool` indicating whether the update was applied, don't throw: a stale packet is a normal, expected occurrence, not an error)
- [ ] Task 2: Application layer — single ingestion path for automatic + manual (AC: #1, #4, #5)
  - [ ] `src/OeeNew.Application/Production/IMachineStateRepository.cs` — `GetAsync(machineId)`, `UpsertAsync(state)` (mirrors `IMachineRepository`'s shape)
  - [ ] `src/OeeNew.Application/Production/IngestProductionReadingUseCase.cs` — single method `IngestAsync(CallerScope scope, IProductionDataSource reading, string? callerRole, CancellationToken)`:
    - Reject if `callerRole` is not `"Operator"` or `"Admin"` (reuse the `MasterDataForbiddenException` — same convention as `MasterDataAuthorization.EnsureAdmin`, just a wider role set since Operators submit readings, not just Admins)
    - Resolve the `Machine` via `IMachineRepository.GetAsync(reading.MachineId)` → `MasterDataParentNotFoundException("Machine", reading.MachineId)` if missing (same exception/status code as Story 1.2's parent-not-found convention → 400 `PARENT_NOT_FOUND`)
    - If `!scope.IsGlobal`: resolve the Machine's parent `Line` via `ILineRepository.GetAsync(machine.LineId)`, then `MasterDataForbiddenException` unless `scope.AllowsSite(line.SiteId) && scope.AllowsLine(machine.LineId)` — identical pattern to `MachineManagementUseCase.ListByLineAsync`
    - Load existing `MachineState` (if any); construct new state on first reading, or call `Apply(...)` on the existing one; `UpsertAsync` regardless of whether `Apply` reports the reading was stale (still worth confirming plumbing works end-to-end, just a no-op on the stored values)
  - [ ] **Do not** add any dependency on a Sync/Central client from this use case or its repository — AC #5 is proven by the *absence* of such a call, not by a runtime "is Central reachable" branch. `IngestProductionReadingUseCase` only ever talks to the local `OeeDbContext` via `IMachineStateRepository`/`IMachineRepository`/`ILineRepository`
- [ ] Task 3: Infrastructure — persistence (AC: #1, #2)
  - [ ] `src/OeeNew.Infrastructure/Persistence/MachineStateRepository.cs` implementing `IMachineStateRepository` against `OeeDbContext` (mirror `MachineRepository.cs`'s shape: thin, no logic)
  - [ ] `OeeDbContext`: add `DbSet<MachineState> MachineStates`; map in `OnModelCreating` — table `MachineState`, `HasKey(s => s.MachineId)` (no separate `Id`; 1:1 with `Machine`), `Counter` as `bigint`, `Status` via `HasConversion<short>()` (match `ReasonCode.LossCategory`'s existing smallint-enum convention, not a string column), `HasOne<Machine>().WithOne().HasForeignKey<MachineState>(s => s.MachineId).OnDelete(DeleteBehavior.Restrict)`
  - [ ] EF Core migration `AddMachineState` (`dotnet ef migrations add AddMachineState --project src/OeeNew.Infrastructure --startup-project src/OeeNew.Api`)
- [ ] Task 4: Api — one ingestion endpoint for both automatic and manual callers (AC: #1, #3, #4)
  - [ ] `src/OeeNew.Api/Controllers/ProductionReadingsController.cs`:
    - `public sealed record IngestReadingRequest(Guid MachineId, DateTimeOffset Timestamp, long Counter, MachineStatus Status) : IProductionDataSource;` — the request DTO implements the Domain interface directly (no separate mapping step); this is what makes "same domain path" for AC #4 structurally true rather than a claim to re-verify later
    - `POST /api/production/readings`, `[Authorize]` (role/scope check happens inside the use case per Task 2, matching how Story 1.6 keeps enforcement in one place instead of duplicating it in both the policy attribute and the use case)
    - Response: `204 NoContent` on success (no read-back needed; the caller already knows what it sent)
  - [ ] Do **not** add a second `/manual-entry` route or an Angular manual-entry form in this story — see Dev Notes "Scope decision: no dedicated manual-entry endpoint"
- [ ] Task 5: Testing (all AC)
  - [ ] `tests/OeeNew.Domain.Tests/Production/MachineStateTests.cs` — first reading stored as-is; newer reading overwrites; older (`reportedAt` before current `LastReportedAt`) reading is ignored (returns `false`, values unchanged)
  - [ ] `tests/OeeNew.Application.Tests/Production/FakeMachineStateRepository.cs` + `IngestProductionReadingUseCaseTests.cs` — `FakeMachineRepository`/`FakeLineRepository` (`OeeNew.Application.Tests.MasterData`) are `internal`, which is assembly-scoped, not namespace-scoped: reference them directly via a `using OeeNew.Application.Tests.MasterData;` in the new test file, don't duplicate them. Only write a new `FakeMachineStateRepository` (no existing equivalent). Cover: unknown machine → `MasterDataParentNotFoundException`; Manager/Viewer role → `MasterDataForbiddenException`; Operator scoped to a different Line → `MasterDataForbiddenException`; valid reading → `MachineState` persisted with correct values; stale reading → prior state untouched
  - [ ] `tests/OeeNew.Api.Tests/Production/ProductionReadingsEndpointsTests.cs` (reuse `MasterDataApiFactory` from `OeeNew.Api.Tests.MasterData` for the Postgres-backed `WebApplicationFactory` + `CreateTokenFor` helper — don't build a second factory): valid Operator/Admin token + real Machine → 204 and a row in `MachineState`; invalid `Status` string in the JSON body → 400 with the standard `VALIDATION_ERROR` envelope (proves AC #3 without any hand-written enum-parsing code — the existing `JsonStringEnumConverter` registered in `Program.cs` plus the `InvalidModelStateResponseFactory` already wired for master-data 400s handles this); Manager/Viewer token → 403; same endpoint called twice back-to-back with different tokens (one "automatic", one representing what a manual-entry caller would send) both land in the same `MachineState` row — this is the test that stands in for AC #4

## Dev Notes

- **Scope decision: no dedicated manual-entry endpoint or Angular form.** AC #4's Given/When/Then describes a future human-facing capability ("Operator/Admin nhập bù... qua form"), but no UX-DR in the epics/UX docs covers this screen (UX-DR6 is the first dashboard-related spec and belongs to Story 2.2). Building a form against no spec would be guessing. What AC #4 actually requires — and what's testable now — is that there is exactly **one** domain path (`IngestProductionReadingUseCase` + `POST /api/production/readings`) for any caller, automatic or manual; a future story can put a thin Angular form in front of this same endpoint once/if a UX spec calls for it. Don't build a second endpoint "for manual entry" — that would be the exact duplication AD-3/AC#4 exist to prevent.
- **No SignalR broadcast in this story.** Story 2.2's own AC explicitly tests "SignalR hub phát broadcast `MachineStatusChanged`" (AD-8) — that wiring (hub, event name, FE subscription) belongs there, not here. This story's job is to make `MachineState` the correct, queryable source of truth that Story 2.2 will read from and broadcast off of. Do not add an `IHubContext` call or a hub project reference to `IngestProductionReadingUseCase`.
- **`counter` is cumulative, never a delta (AC #2).** Don't subtract from a previous value or compute a difference anywhere in this story — just store the latest `Counter` as received. Delta/rate calculations for OEE math are out of scope here (Epic 3/4 territory) and depend on decisions not yet made about aggregation windows.
- **Status enum validation is "free" via existing wiring, don't hand-roll it.** `Program.cs` already registers `JsonStringEnumConverter()` globally and `InvalidModelStateResponseFactory` already returns the standard `{ code: "VALIDATION_ERROR", ... }` envelope for any model-binding failure (see the master-data 400 tests for the pattern). Binding `IngestReadingRequest.Status` directly to the `MachineStatus` enum means an unrecognized string is rejected before the controller action even runs — no manual `Enum.IsDefined` check needed in this story, unlike `ReasonCode`'s constructor-time check (that one guards a value set at construction time from *validated* C# code, not from raw wire input).
- **Out-of-order guard is new territory — not copied from Epic 1.** Nothing in Epic 1 handles "a later write arrives with an earlier timestamp" because master-data writes are user-driven one-at-a-time. Ingestion is push-based and networked (AC #5's site-autonomy framing exists precisely because ingestion must tolerate real-world delivery issues), so `MachineState.Apply` must silently drop stale updates rather than let them regress the visible state — this directly protects Story 2.2/2.3's dashboard from flapping backward.
- **AC #5 (site autonomy) is a negative-space requirement.** There is no Sync/Central client to call yet (Epic 5 territory, still `Deferred` in the Architecture Spine) — proving this AC means confirming `IngestProductionReadingUseCase` has zero such dependency, not adding a "central down → still works" branch. If a future Sync module is added in Epic 5, it must pull from the site's local tables asynchronously; it must never sit in the ingestion write path.
- **Reuse, don't rebuild, the scope-check shape from Story 1.6.** `CallerScope.AllowsSite`/`AllowsLine` and the "resolve parent, then check scope, else `MasterDataForbiddenException`" pattern are already established by `MachineManagementUseCase.ListByLineAsync` (`src/OeeNew.Application/MasterData/MachineManagementUseCase.cs:9-22`) and enforced end-to-end by `CallerScope` (`src/OeeNew.Application/Auth/CallerScope.cs`). This story is the first time that pattern gates a **write** made by a non-Admin role (Operator) rather than only filtering **reads** — the mechanism doesn't change, only which operation it guards.
- **JSON field casing follows the existing API convention, not the PRD addendum's literal `machine_id`.** The addendum's "JSON: machine_id, timestamp, counter, status" is informal brainstorm wording from before the Architecture Spine existed — every other endpoint in this codebase (`CreateMachineRequest`, etc.) uses ASP.NET Core's default camelCase serialization with no per-request casing override in `Program.cs`. `IngestReadingRequest` should follow suit: `{ "machineId": ..., "timestamp": ..., "counter": ..., "status": "Running" }`, not snake_case — introducing a one-off snake_case endpoint would be an inconsistency, not a requirement.
- **Role check is intentionally in the use case, not a second `[Authorize(Policy=...)]`.** Epic 1's convention puts the Admin-only check inside the use case (`MasterDataAuthorization.EnsureAdmin`) as the source of truth, with the controller attribute as a coarse first filter for writes. Here there's no existing `[Authorize(Policy = "OperatorOrAdmin")]` policy in `Program.cs`, and adding one just to duplicate a check already needed inside the use case (for the scope-check, which policies can't express) would be redundant — keep `[Authorize]` (any authenticated caller) on the controller and let the use case be the single place that decides role + scope.

### Project Structure Notes

- New `Production` subfolder alongside the existing `Identity`/`MasterData` ones in `Domain`, `Application`, and mirrored in `Infrastructure/Persistence` (flat, like `MachineRepository.cs` already is) and `Api/Controllers` — consistent with the Architecture Spine's source tree, which doesn't enumerate every backend folder but establishes the `Domain → Application → Infrastructure/Api` layering these must respect (AD-1, enforced by `tests/OeeNew.Architecture.Tests/LayerDependencyTests.cs` — run it, don't just assume compliance).
- No new `web/oee-shell` files in this story — see the manual-entry scope decision above. `src/app/pages/dashboard/dashboard-page.ts` stays the untouched placeholder it is today; Story 2.2 is what fills it in.
- `MachineState` is a **new table**, not a column added to `Machine` — `Machine` is Epic 1's static master-data identity (name + parent Line), while `MachineState` is Epic 2's mutable, frequently-overwritten runtime state. Conflating them would mean every ingestion write touches the master-data table Epic 1 already has read-scope-filtering and tests built around.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2] — Story 2.1 full AC
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-3] — Ingestion Adapter Pattern, `IProductionDataSource` shape, cumulative counter, fixed status enum
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-2] — site autonomy, what Sync does and doesn't do (AC #5)
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Consistency-Conventions] — API error envelope, ISO 8601 UTC timestamps
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/addendum.md#Quyết-định-Ingestion-Adapter-Pattern] — why no real protocol adapter yet, temporary ingestion source question
- [Source: _bmad-output/implementation-artifacts/1-6-site-line-selector-scope-enforcement.md] — `CallerScope`/scope-check pattern this story reuses for a write instead of a read
- [Source: src/OeeNew.Application/MasterData/MachineManagementUseCase.cs] — the exact scope-check shape being mirrored
- [Source: src/OeeNew.Api/Program.cs] — existing `JsonStringEnumConverter` + `InvalidModelStateResponseFactory` wiring that satisfies AC #3 without new code
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — pattern for where to log any new deferred items this story surfaces (e.g., a future manual-entry Angular form, once a UX-DR exists for it)

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
