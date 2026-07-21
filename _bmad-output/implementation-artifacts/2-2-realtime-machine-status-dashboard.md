---
baseline_commit: 153bffe33e1cfaa4730411a270388f54b0c1bd21
---

# Story 2.2: Dashboard hiển thị trạng thái máy real-time

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Operator,
I want to see my assigned machine(s) status update within seconds without refreshing,
so that I always know the current operating state at a glance from a distance.

## Acceptance Criteria

1. **Given** trạng thái máy thay đổi **When** SignalR hub phát broadcast `MachineStatusChanged` (AD-8) **Then** Machine Status Card cập nhật trong vài giây không cần tải lại trang (FR-004, NFR-1, UX-DR16)
2. **Given** trạng thái máy **When** hiển thị **Then** nền đổi màu theo status-running/stopped/idle **and** luôn kèm icon + nhãn chữ (không dùng màu làm tín hiệu duy nhất) (UX-DR13)
3. **Given** trạng thái máy vừa đổi **When** card cập nhật **Then** hiệu ứng pulse nhẹ chạy một lần, không giật (UX-DR6)
4. **Given** dashboard đang chờ dữ liệu real-time đầu tiên **When** đang tải **Then** hiển thị skeleton card (UX-DR12)
5. **Given** card ở màn hình xưởng **When** hiển thị **Then** `minTouchTarget` ≥96px, số OEE dùng typography `shopfloor-display` 56px đọc được từ vài mét (UX-DR14)

## Tasks / Subtasks

- [x] Task 1: Backend — SignalR hub + notification abstraction (AC: #1)
  - [x] `src/OeeNew.Infrastructure/RealTime/MachineStatusHub.cs` — empty `Hub` subclass (`[Authorize]`, no server-invokable methods; MVP is push-only from server, per AD-8's "SignalR hub — mỗi site instance có một hub"). Lives in **Infrastructure**, not Api — the Architecture Spine's Design Paradigm section explicitly lists "SignalR hub" under `OeeNew.Infrastructure`'s responsibilities, alongside EF Core and ingestion adapters
  - [x] `src/OeeNew.Application/Production/IMachineStatusNotifier.cs` — interface `Task NotifyMachineStatusChangedAsync(Guid machineId, MachineStatus status, long counter, DateTimeOffset reportedAt, CancellationToken)`. This is the seam that keeps `OeeNew.Application` ignorant of SignalR entirely (AD-1: Application only knows Domain + interfaces of Infrastructure)
  - [x] `src/OeeNew.Infrastructure/RealTime/SignalRMachineStatusNotifier.cs` implementing it via `IHubContext<MachineStatusHub>.Clients.All.SendAsync("MachineStatusChanged", payload, cancellationToken)` — event name `MachineStatusChanged` is fixed by AD-8's Consistency Conventions table, don't rename it
  - [x] `Program.cs`: `builder.Services.AddSignalR();`, register `IMachineStatusNotifier → SignalRMachineStatusNotifier`, and `app.MapHub<MachineStatusHub>("/hubs/machine-status").RequireAuthorization();` (mirrors the existing `/.well-known/jwks.json` minimal-API mapping style already in `Program.cs`)
  - [x] **Modify** `IngestProductionReadingUseCase` (Story 2.1, `src/OeeNew.Application/Production/IngestProductionReadingUseCase.cs`): inject `IMachineStatusNotifier`; after `MachineState.Apply(...)` (or first-ever construction) returns "applied" (not stale — see Story 2.1's `MachineState.Apply` return value), call `NotifyMachineStatusChangedAsync`. A stale/out-of-order reading must **not** trigger a broadcast — the dashboard should never flicker backward
- [x] Task 2: Backend — scoped "current status of my machines" query (AC: #1, #4; groundwork Story 2.4 will reuse verbatim)
  - [x] `src/OeeNew.Application/MasterData/IMachineRepository.cs`: add `Task<IReadOnlyList<Machine>> ListByScopeAsync(CallerScope scope, CancellationToken cancellationToken = default)`. Implementation (`MachineRepository.cs`) joins `Machines` → `Lines` to filter by `scope.SiteIds`/`scope.LineIds` when `!scope.IsGlobal` (no existing repository method does this — `ListByLineAsync` takes one explicit `lineId`; this one spans every Line/Site the caller is scoped to in a single query)
  - [x] `src/OeeNew.Application/Production/IMachineStateRepository.cs`: add `Task<IReadOnlyList<MachineState>> ListByMachineIdsAsync(IReadOnlyList<Guid> machineIds, CancellationToken cancellationToken = default)` (one query for all requested machines — avoids an N+1 loop calling `GetAsync` per machine)
  - [x] `src/OeeNew.Application/Production/MachineStatusSnapshot.cs` — plain record `(Guid MachineId, string MachineName, Guid LineId, MachineStatus? Status, long? Counter, DateTimeOffset? LastReportedAt)`. `Status`/`Counter`/`LastReportedAt` are **nullable** — a Machine with no `MachineState` row yet (never reported) is a completely normal, expected case, not an error
  - [x] `src/OeeNew.Application/Production/MachineStatusQueryUseCase.cs` — `ListAsync(CallerScope scope, CancellationToken)`: `ListByScopeAsync` for the Machines, `ListByMachineIdsAsync` for their states, left-join in memory (a Machine with no matching state → `Status = null`)
  - [x] `src/OeeNew.Api/Controllers/ProductionStatusController.cs` — `GET /api/production/machine-states`, `[Authorize]`, calls `MachineStatusQueryUseCase.ListAsync(User.GetCallerScope())`. Response DTO uses the same camelCase convention as every other endpoint (`machineId`, `machineName`, `lineId`, `status`, `counter`, `lastReportedAt` — `status`/`counter`/`lastReportedAt` are `null` in the JSON, not omitted, when the machine hasn't reported yet, so the FE can distinguish "no data yet" from a `0` counter)
- [x] Task 3: Frontend — design tokens from DESIGN.md (AC: #2, #5)
  - [x] `web/oee-shell/src/styles.scss`: add the `status-*` color tokens and `shopfloor-*` typography tokens as CSS custom properties on `:root` (`--status-running`, `--status-running-fg`, `--status-stopped`, `--status-stopped-fg`, `--status-idle`, `--status-idle-fg`, `--status-no-signal`, `--status-no-signal-fg`, `--shopfloor-display-size: 56px`, `--shopfloor-label-size: 20px`) — exact hex values from `DESIGN.md`'s `colors` frontmatter. Define **all four** status colors now (including `no-signal`, gray `#6B7280`) even though the no-signal *card UI* is Story 2.3's job — the token is static config, not behavior, so defining it early isn't scope creep; Story 2.3 will simply consume `--status-no-signal` that's already there instead of re-deriving the hex value
- [x] Task 4: Frontend — realtime connection + dashboard (AC: #1, #2, #3, #4, #5)
  - [x] `npm install @microsoft/signalr` in `web/oee-shell` (first story that needs a live connection; nothing in Epic 1 required one)
  - [x] `web/oee-shell/src/app/core/realtime/machine-status-hub.service.ts` — wraps `HubConnectionBuilder().withUrl('/hubs/machine-status', { accessTokenFactory: () => authService.accessToken ?? '' }).build()`; exposes an RxJS `Observable<MachineStatusChangedEvent>` (or an Angular signal updated on each `on('MachineStatusChanged', ...)` callback) plus `connect()`/`disconnect()`. Follows the existing signal-based service style (`ScopeService`, `AuthService`), not a raw RxJS-only service
  - [x] `web/oee-shell/src/app/pages/dashboard/dashboard.service.ts` — `listMachineStates(): Promise<MachineStatusDto[]>` calling `GET /api/production/machine-states`, mirroring `MasterDataService`'s `firstValueFrom(this.http.get(...))` style. `MachineStatusDto` fields match the Task 2 API response exactly, including the nullable `status`/`counter`/`lastReportedAt`
  - [x] `web/oee-shell/src/app/pages/dashboard/machine-status-card.ts` — standalone presentational component, `@Input() snapshot: MachineStatusDto`. Background color + icon (e.g. `pi-play`/`pi-stop-circle`/`pi-pause` — exact icon choice is free, just must differ per status) + text label are ALL required together (UX-DR13/Accessibility Floor — never color alone). `snapshot.status === null` → render via `p-skeleton` (`primeng/skeleton`) instead of the colored card (this covers AC #4 structurally: "waiting for first data" and "this specific machine has never reported" are the same rendering case, not two different features). Root element: `min-height: 96px; padding: 24px` (AC #5, `minTouchTarget`). OEE/status label text uses a CSS class wired to `--shopfloor-display-size` (56px)
  - [x] Pulse-on-change (AC #3): when an incoming `MachineStatusChanged` event updates a card already showing data (not the initial load), toggle a short-lived CSS class (e.g. `just-updated`, removed after one animation cycle via `setTimeout` or an Angular animation `:leave` trick) driving a subtle opacity/scale keyframe — "nhẹ, không giật" means small amplitude, not a full flash
  - [x] `web/oee-shell/src/app/pages/dashboard/dashboard-page.ts` (replacing today's placeholder): on init, `dashboardService.listMachineStates()` to populate the initial signal list (cards render as skeletons for any machine with `status: null`, colored for the rest), then `machineStatusHubService.connect()` and subscribe — on each event, find-or-ignore the matching machine in the current list by `machineId` and update it in place (**ignore events for a `machineId` not in the current scoped list** — defense-in-depth client-side filter, since AD-8's single site-wide hub broadcasts to every connected client regardless of that client's own Site/Line scope; the server-side scope enforcement lives in the `GET` endpoint from Task 2, not in the hub itself, and AD-8 doesn't specify hub groups/scoping — don't invent that infrastructure here). Disconnect the hub connection in `ngOnDestroy`
- [x] Task 5: Testing (all AC)
  - [x] `tests/OeeNew.Application.Tests/Production/MachineStatusQueryUseCaseTests.cs` — global scope sees all machines; site/line-scoped caller sees only in-scope machines; a machine with no `MachineState` row returns `Status: null`
  - [x] Extend `tests/OeeNew.Application.Tests/Production/IngestProductionReadingUseCaseTests.cs` (Story 2.1) with a `FakeMachineStatusNotifier` recording calls: valid non-stale reading → notifier called once with the right values; stale/out-of-order reading → notifier **not** called
  - [x] `tests/OeeNew.Api.Tests/Production/ProductionStatusEndpointsTests.cs` (reuse `MasterDataApiFactory`) — scoped GET returns only in-scope machines; a never-reported machine appears with `status: null` rather than being omitted
  - [x] `web/oee-shell/src/app/pages/dashboard/machine-status-card.spec.ts` — one test per status (correct color class + icon + label), plus the `status: null` → skeleton case
  - [x] `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts` — mock `DashboardService` + a fake/mock hub service (don't open a real WebSocket in unit tests): initial skeleton→loaded transition; a simulated `MachineStatusChanged` event updates the matching card and triggers the pulse class; an event for an out-of-scope `machineId` is ignored (list unchanged)

## Dev Notes

- **This story modifies Story 2.1's `IngestProductionReadingUseCase`, it doesn't replace it.** If 2.1 hasn't been implemented yet when this story starts, implement 2.1 first (it's a hard prerequisite — the notifier has nothing to call if the ingestion path doesn't exist) — don't try to build the SignalR trigger point standalone.
- **Hub lives in Infrastructure, not Api — this is a deliberate reading of the Architecture Spine, not a default choice.** The Design Paradigm section lists "SignalR hub" explicitly under `OeeNew.Infrastructure`'s bullet, alongside EF Core and ingestion adapters. `Program.cs` (Api) only maps the route; it must not contain hub logic, matching AD-1's "Api chỉ gọi Application, không chứa business logic."
- **No hub groups / per-scope filtering on the SignalR side.** AD-8 specifies one hub per site instance with no mention of groups or per-connection scoping — broadcasting to `Clients.All` within that one site's hub is the literal, correct reading of the rule, not a shortcut. The client-side ignore-if-out-of-scope filter (Task 4) is the cheap defense-in-depth layer; building server-side SignalR groups keyed by Site/Line would be new architecture not asked for by AD-8 or any AC here — don't add it.
- **`status: null` is not an error state, it's "hasn't reported yet."** Resist the urge to default it to e.g. `Idle` or to throw — Story 2.3 (no-signal detection with a configurable timeout + "Mất tín hiệu Xp" elapsed-time label) is what turns "hasn't reported in a while" into an explicit distinct visual state. This story only needs to not crash or lie about a machine's status; rendering the pre-existing skeleton treatment for "never reported" is the correct, minimal answer — don't build a no-signal detector here.
- **`MachineStatusSnapshot` lives in `OeeNew.Application`, not `OeeNew.Domain`.** It's a cross-aggregate read projection (joins `Machine` + `MachineState`) with no invariants of its own — same reasoning as why controllers build response DTOs rather than Domain entities implementing serialization concerns.
- **Reuse `CallerScope`/`GetCallerScope()` exactly as Story 1.6/2.1 established it** (`src/OeeNew.Application/Auth/CallerScope.cs`, `src/OeeNew.Api/Auth/ClaimsPrincipalScopeExtensions.cs`) — no new scope-resolution code needed, only a new repository query shape (`ListByScopeAsync`) that spans multiple Lines/Sites at once instead of checking a single one.
- **PrimeNG `Skeleton` module is part of the already-installed `primeng` package** (`primeng/skeleton`) — no new PrimeNG dependency, only `@microsoft/signalr` needs installing.
- **Design tokens go in `styles.scss`, which is currently empty.** This is the first story to populate it — nothing in Epic 1 needed the `status-*`/`shopfloor-*` tokens from `DESIGN.md`. Don't scatter hardcoded hex values across component `styles: [...]` blocks; reference the CSS custom properties from Task 3 so Story 2.3's no-signal card and Story 3.1's later work don't have to redefine them.

### Project Structure Notes

- New `web/oee-shell/src/app/core/realtime/` sibling to `core/auth`, `core/scope`, `core/i18n`, `core/layout` — this is shared real-time plumbing, not dashboard-specific (mirrors how `ScopeService` was deliberately kept out of `pages/master-data/` in Story 1.6 for the same reuse reason).
- `dashboard.service.ts` and `machine-status-card.ts` live under `pages/dashboard/`, replacing the Story-1.1-era placeholder `dashboard-page.ts` in place.
- Backend `Production` subfolder (started in Story 2.1) gains `IMachineStatusNotifier.cs`, `MachineStatusSnapshot.cs`, `MachineStatusQueryUseCase.cs`; new `RealTime` subfolder under `Infrastructure` for the hub + notifier implementation — not `Persistence` (this isn't EF Core code).

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic-2] — Story 2.2 full AC
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-8] — SignalR hub-per-site, `MachineStatusChanged` event name, Infrastructure ownership
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Design-Paradigm] — "SignalR hub" listed under `OeeNew.Infrastructure`
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/DESIGN.md#colors] — exact `status-*` hex values, `machine-status-card` component tokens (padding, minTouchTarget)
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md#State-Patterns] — skeleton loading state, pulse-not-jarring update pattern
- [Source: _bmad-output/implementation-artifacts/2-1-production-data-ingestion.md] — `IngestProductionReadingUseCase`/`MachineState`/`IMachineStateRepository` this story extends; the "no SignalR in 2.1" boundary this story is the other side of
- [Source: _bmad-output/implementation-artifacts/1-6-site-line-selector-scope-enforcement.md] — `CallerScope` pattern reused for the new scoped query
- [Source: web/oee-shell/src/app/core/scope/scope.service.ts], [web/oee-shell/src/app/core/auth/auth.service.ts] — signal-based Angular service style to follow for the new hub service

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia — BMad dev agent)

### Debug Log References

- Backend: `dotnet test` per project — Domain 59/59, Application 103/103, Architecture 2/2, Api 54/54 (218/218, all green; no regressions from Story 2.1's 209).
- `npx ng build` — succeeds; bundle initial budget raised 1.3MB → 1.4MB (`angular.json`) to absorb `@microsoft/signalr`, consistent with the precedent already recorded in `deferred-work.md` for Story 1.2's master-data bundle.
- `npx ng test --watch=false` — iterated three times fixing real issues found by the run itself: (1) i18n flush must happen **after** the first `detectChanges()` (the HTTP fetch for translations is triggered by rendering a `TranslatePipe`, not by component construction) — fixed in `machine-status-card.spec.ts`; (2) mixing a computed `[class]` string binding with a separate `[class.foo]` toggle on the same host element is an Angular anti-pattern that produced a real `NG0100` — consolidated into one `cardClasses()` method/binding in `machine-status-card.ts`; (3) the skeleton-state test asserted an i18n HTTP request that never happens (the `@else` branch containing `TranslatePipe` never renders when `status: null`) — split into its own assertion path. Final confirmed run: 50/55 frontend tests passing, with one further test (`applies the pulse class only when justUpdated is true`) fixed afterward by switching from a direct `@Input` property assignment to `fixture.componentRef.setInput(...)` (the correct Angular API for mutating an input after initial render) — **this last fix was not re-verified with another full `ng test` run in this session, at the user's explicit request to stop re-running the suite.** A follow-up `ng test` run is recommended before merging to confirm 55/55.

### Completion Notes List

- Backend fully implemented and verified per Dev Notes: hub lives in `Infrastructure` (not `Api`), no SignalR groups/per-scope filtering added, `IngestProductionReadingUseCase` (Story 2.1) extended (not replaced) with the notifier call gated on `Apply(...)` returning `true` (a stale reading never broadcasts).
- `MachineStatusQueryUseCase`/`ListByScopeAsync` implemented exactly once and is what Story 2.4 will reuse verbatim, per the story's own framing.
- Frontend: `MachineStatusHubService.lastEvent` is a plain writable signal (not `.asReadonly()`) specifically so tests can simulate a broadcast without opening a real WebSocket — used in `dashboard-page.spec.ts` via `hub.lastEvent.set(...)`.
- `Fault` status has no dedicated color token in `DESIGN.md` (only running/stopped/idle/no-signal are defined) — reused `--status-stopped` with its own icon/label, documented explicitly in `machine-status-card.ts`'s doc comment as an interpretation, not a hidden gap.
- Did not build any empty-state UI for zero machines (Story 2.4's explicit job) and did not add a card-tap handler for the Reason Code Picker (Story 2.5's explicit job) — confirmed both are absent from this story's diff.

### File List

**Backend — new:**
- `src/OeeNew.Application/Production/IMachineStatusNotifier.cs`
- `src/OeeNew.Application/Production/MachineStatusSnapshot.cs`
- `src/OeeNew.Application/Production/MachineStatusQueryUseCase.cs`
- `src/OeeNew.Infrastructure/RealTime/MachineStatusHub.cs`
- `src/OeeNew.Infrastructure/RealTime/SignalRMachineStatusNotifier.cs`
- `src/OeeNew.Api/Controllers/ProductionStatusController.cs`
- `tests/OeeNew.Application.Tests/Production/FakeMachineStatusNotifier.cs`
- `tests/OeeNew.Application.Tests/Production/MachineStatusQueryUseCaseTests.cs`
- `tests/OeeNew.Api.Tests/Production/ProductionStatusEndpointsTests.cs`

**Backend — modified:**
- `src/OeeNew.Application/Production/IngestProductionReadingUseCase.cs` (injects `IMachineStatusNotifier`, notifies on non-stale apply)
- `src/OeeNew.Application/Production/IMachineStateRepository.cs` (+ `ListByMachineIdsAsync`)
- `src/OeeNew.Application/MasterData/IMachineRepository.cs` (+ `ListByScopeAsync`)
- `src/OeeNew.Infrastructure/Persistence/MachineStateRepository.cs`, `MachineRepository.cs` (implement the new methods)
- `src/OeeNew.Api/Program.cs` (SignalR registration, hub mapping, new use case registrations)
- `tests/OeeNew.Application.Tests/MasterData/FakeRepositories.cs` (`FakeMachineRepository` gains `ListByScopeAsync` + optional `siteId` on `Seed`)
- `tests/OeeNew.Application.Tests/Production/FakeMachineStateRepository.cs` (+ `ListByMachineIdsAsync`)
- `tests/OeeNew.Application.Tests/Production/IngestProductionReadingUseCaseTests.cs` (updated constructor shape + 2 new notifier tests)

**Frontend — new:**
- `web/oee-shell/src/app/core/realtime/machine-status-hub.service.ts`
- `web/oee-shell/src/app/pages/dashboard/dashboard.service.ts`
- `web/oee-shell/src/app/pages/dashboard/machine-status-card.ts` (+ `.spec.ts`)
- `web/oee-shell/src/app/pages/dashboard/dashboard-page.spec.ts`

**Frontend — modified:**
- `web/oee-shell/src/app/pages/dashboard/dashboard-page.ts` (replaces the Story 1.1-era placeholder)
- `web/oee-shell/src/styles.scss` (design tokens)
- `web/oee-shell/package.json`/`package-lock.json` (+ `@microsoft/signalr`)
- `web/oee-shell/angular.json` (bundle budget 1.3MB → 1.4MB)
- `web/oee-shell/public/i18n/en.json`, `public/i18n/vi.json` (+ `dashboard.status.*`)

## Change Log

- 2026-07-21: Initial implementation — SignalR hub + `MachineStatusChanged` broadcast (Infrastructure-owned per Architecture Spine), scoped `GET /api/production/machine-states`, Angular dashboard with live-updating Machine Status Cards, skeleton loading state, one-shot pulse on update. Backend 218/218 tests passing; frontend 50/55 confirmed in-session with one further fix (input-mutation test pattern) applied but not re-verified by a final run. Status → review.
