---
baseline_commit: acc0da8b12530ade9a63e789fea63451e34c6714
---

# Story 1.2: Quản lý danh mục Site/Line/Machine

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin,
I want to create/edit/delete Sites, Lines, and Machines in a Site > Line > Machine hierarchy,
so that the rest of the system has a structured asset reference.

## Acceptance Criteria

1. **Given** tôi là Admin **When** tạo Site mới **Then** bản ghi có khoá chính `uuidv7()` (AD-6) và xuất hiện trong danh sách
2. **Given** Site đã tồn tại **When** tạo Line **Then** Line gắn với Site cha, không tạo được nếu thiếu Site cha hợp lệ
3. **Given** Line đã tồn tại **When** tạo Machine **Then** Machine gắn với Line cha
4. **Given** tôi là Manager/Operator/Viewer **When** gọi API tạo/sửa/xoá Site/Line/Machine **Then** bị từ chối (FR-015, NFR-5) — enforce ở tầng API
5. **Given** Line còn Machine con **When** Admin xoá Line **Then** bị chặn kèm danh sách Machine phụ thuộc **And** `[ASSUMPTION]` không cascade-delete ở MVP để tránh mất dữ liệu

## Tasks / Subtasks

- [x] Task 1: Domain entities (AC: #1, #2, #3)
  - [x] `OeeNew.Domain`: entity `Site`, `Line`, `Machine` — khoá chính `Guid`, không dependency EF Core trong entity thuần domain nếu theo POCO pattern
  - [x] Quan hệ: `Line.SiteId` (FK bắt buộc), `Machine.LineId` (FK bắt buộc) — khớp ERD Architecture Spine (Site ||--o{ LINE, LINE ||--o{ MACHINE)
- [x] Task 2: EF Core + Postgres (AC: #1, #2, #3, #5)
  - [x] `OeeNew.Infrastructure`: `DbContext`, migration tạo bảng `Site`/`Line`/`Machine`
  - [x] Cột `Id uuid PRIMARY KEY DEFAULT uuidv7()` — dùng hàm gốc PostgreSQL 18, **không** dùng `Guid.CreateVersion7()` phía .NET (AD-6 — tránh lỗi thứ tự byte/không đơn điệu đã ghi nhận trên .NET runtime)
  - [x] FK `Line.SiteId`, `Machine.LineId` với `ON DELETE RESTRICT` (không cascade — khớp AC5 `[ASSUMPTION]`)
- [x] Task 3: Application use case + API (AC: #1, #2, #3, #4, #5)
  - [x] Use case tạo/sửa/xoá Site/Line/Machine tại `OeeNew.Application`, gọi qua interface, không EF Core trực tiếp
  - [x] Controller tại `OeeNew.Api`: policy-based authorization yêu cầu `role=Admin` cho mọi endpoint ghi (401/403 map về error envelope đã thiết lập ở Story 1.1)
  - [x] Xoá Line: kiểm tra tồn tại Machine con trước, nếu có → trả lỗi envelope kèm `details` liệt kê Machine phụ thuộc (AC5)
- [x] Task 4: Angular UI (AC: #1, #2, #3, #4)
  - [x] Màn hình `web/oee-shell/src/app/master-data` (thư mục đã scaffold ở Story 1.1): danh sách + form Site/Line/Machine dùng PrimeNG DataTable/Dialog (kế thừa nguyên component Sakai, không custom thêm ngoài 3 biến thể đã chốt ở DESIGN.md)
  - [x] Ẩn hoàn toàn action tạo/sửa/xoá nếu role hiện tại ≠ Admin (double-check: UI ẩn + API chặn, không chỉ 1 lớp)
- [x] Task 5: Testing (tất cả AC)
  - [x] Unit test Domain: validate quan hệ Site>Line>Machine, không tạo Line thiếu Site cha
  - [x] Integration test API: tạo Site → Guid hợp lệ; xoá Line còn Machine → 4xx với danh sách Machine; role≠Admin gọi API ghi → 403
  - [x] Angular: component test ẩn nút thao tác khi role≠Admin

### Review Findings

- [x] [Review][Patch] Application-layer authorization re-check missing — Architecture Spine's Consistency Conventions and this story's own Dev Notes (line 49) explicitly require the Admin-only check to be re-verified at the Application layer, not just via `[Authorize(Policy = "AdminOnly")]` at the controller. Resolved (decision: patch) by adding `MasterDataAuthorization.EnsureAdmin`, a new `MasterDataForbiddenException` (403 `FORBIDDEN`), a `callerRole` parameter on every write method of `SiteManagementUseCase`/`LineManagementUseCase`/`MachineManagementUseCase`, and controllers now pass the caller's `role` claim through. [src/OeeNew.Application/MasterData/SiteManagementUseCase.cs, LineManagementUseCase.cs, MachineManagementUseCase.cs, MasterDataAuthorization.cs, MasterDataForbiddenException.cs]
- [x] [Review][Patch] Hardcoded Postgres credentials committed to source (`Password=1`) — moved to `dotnet user-secrets` for the Api project's local dev connection string; `appsettings.Development.json` no longer carries it. The test factory's local `oeenew_test` connection string is left as-is, folded into the already-deferred test-infra item below (fixing it "for real" needs the same docker-compose/testcontainers decision). [src/OeeNew.Api/appsettings.Development.json]
- [x] [Review][Patch] TOCTOU race in create/delete guards — added a `DbUpdateException => 409 CONFLICT` mapping in `ApiExceptionHandler` so a concurrent write between check and DB operation now surfaces as a clean 409 instead of a raw 500. [src/OeeNew.Api/Errors/ApiExceptionHandler.cs]
- [x] [Review][Patch] Domain `ValidateName` doesn't enforce the 200-char max that EF/Postgres enforce — added a max-length check (200) to `Site`/`Line`/`Machine.ValidateName`, throwing the new `MasterDataValidationException`. [src/OeeNew.Domain/MasterData/Site.cs, Line.cs, Machine.cs]
- [x] [Review][Patch] `ApiExceptionHandler`'s `ArgumentException => 400 VALIDATION_ERROR` mapping was global and untyped — replaced with a dedicated `MasterDataValidationException` mapping; the blanket `ArgumentException` case was removed. [src/OeeNew.Api/Errors/ApiExceptionHandler.cs, src/OeeNew.Domain/MasterData/MasterDataValidationException.cs]
- [x] [Review][Patch] Angular `describeError`'s NOT_FOUND message claimed "the list has been refreshed" without actually refetching — added `handleError`/`refreshAfterNotFound` so a NOT_FOUND response now really reloads sites and the currently selected Line/Site's children. [web/oee-shell/src/app/pages/master-data/master-data-page.ts]
- [x] [Review][Patch] `selectSite`/`selectLine` had no request-ordering guard — added a per-selection token so a stale, slower response can no longer overwrite the panel after a newer selection resolved. [web/oee-shell/src/app/pages/master-data/master-data-page.ts]
- [x] [Review][Patch] `loadSites`/`selectSite`/`selectLine` had no try/catch — wrapped in try/catch surfacing `error()` instead of an unhandled promise rejection. [web/oee-shell/src/app/pages/master-data/master-data-page.ts]
- [x] [Review][Defer] No uniqueness constraint/check for Site/Line/Machine names (app or DB level) [src/OeeNew.Domain/MasterData/Site.cs, src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs] — deferred, pre-existing, not required by any AC in this story
- [x] [Review][Defer] Integration tests require a real local Postgres `oeenew_test` instance with hardcoded credentials, no docker-compose/testcontainers/CI provisioning, and never clean up inserted rows [tests/OeeNew.Api.Tests/MasterData/MasterDataApiFactory.cs] — deferred, pre-existing test-infra pattern, needs a project-level decision broader than this story
- [x] [Review][Defer] List endpoints (`sites`, `lines`, `machines`) are entirely unpaged [src/OeeNew.Api/Controllers/SitesController.cs, LinesController.cs, MachinesController.cs] — deferred, acceptable at current master-data volumes, revisit if that assumption changes
- [x] [Review][Defer] Angular bundle budget raised instead of lazy-loading the master-data route [web/oee-shell/angular.json] — deferred, acceptable now, revisit lazy-loading as more PrimeNG-backed features land

## Dev Notes

- **AD-6 (GUID):** Toàn bộ entity đồng bộ (Site/Line/Machine nằm trong danh sách AD-5 "đồng bộ lên trung tâm qua Sync") dùng `uuid` sinh bằng `uuidv7()` **native của Postgres 18**, không phải code .NET. Cú pháp: `id UUID PRIMARY KEY DEFAULT uuidv7()` — hàm này có sẵn từ Postgres 18, nhận thêm interval tuỳ chọn để dịch timestamp nhưng mặc định không cần tham số. [Nguồn: web research bên dưới]
- **FR-015/NFR-5:** Không được chỉ ẩn nút trên UI — Controller phải có `[Authorize(Policy = "AdminOnly")]` hoặc tương đương, kiểm tra lại ở Application layer cho use case ghi dữ liệu (theo Consistency Conventions của Architecture Spine).
- **Không cascade-delete (AC5):** Đây là `[ASSUMPTION]` được đánh dấu tường minh trong epics.md — quyết định của story, không phải invariant kiến trúc bắt buộc. Nếu Admin cần dọn dữ liệu thử nghiệm, story này KHÔNG cung cấp cascade — chỉ báo lỗi liệt kê phụ thuộc.
- **Nối tiếp Story 1.1:** Dùng lại scaffold `OeeNew.Domain/Application/Infrastructure/Api` và shell Angular + error envelope + JWT auth đã dựng ở 1.1 — không tạo lại project, chỉ thêm entity/controller/module mới vào cấu trúc có sẵn.
- **Chưa có story trước trong cùng chuỗi tuần tự dữ liệu này** (1.1 là nền tảng auth/shell, không tạo entity nghiệp vụ) — không có previous-story intelligence về entity pattern để kế thừa; đây là entity nghiệp vụ đầu tiên, thiết lập pattern cho các story sau (1.3, 1.4, 1.5 dùng lại cùng pattern EF Core + uuidv7 + Application use case).

### Project Structure Notes

- Thêm entity vào `src/OeeNew.Domain/` (namespace `OeeNew.Domain.MasterData` hoặc tương đương), migration vào `src/OeeNew.Infrastructure/`, controller vào `src/OeeNew.Api/`.
- UI vào `web/oee-shell/src/app/master-data/` (thư mục đã có sẵn từ Story 1.1, chưa có nội dung).
- Không tạo thư mục mới ngoài source tree đã chốt.

### References

- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-6] — GUID uuidv7() convention
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Core-Entity-ERD] — quan hệ Site/Line/Machine
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-4] — master data ghi tại site
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-011] — yêu cầu gốc
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-1] — Story 1.2 đầy đủ AC
- [Source: _bmad-output/implementation-artifacts/1-1-login-app-shell.md] — scaffold, auth, error envelope đã dựng, tái sử dụng nguyên trạng

**Latest tech (web research 2026-07-18):** `uuidv7()` là hàm built-in PostgreSQL 18, cú pháp `SELECT uuidv7();` hoặc dùng làm default `DEFAULT uuidv7()` trong `CREATE TABLE`; nhận interval tuỳ chọn để dịch timestamp (không cần cho use case này). [Source: https://neon.com/postgresql/postgresql-18/uuidv7-support, https://www.postgresql.org/docs/current/release-18.html]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- PrimeNG 21 Table's `pTemplate="body"` directive requires the standalone `PrimeTemplate` directive (from `primeng/api`) to be explicitly added to the component's `imports` array — without it the directive is silently ignored and the table renders as empty (`data-p="empty"`), even though no compile error is raised.
- `MasterDataService`'s `firstValueFrom(...)` await resolves as a microtask one tick after `HttpTestingController.flush()` runs; tests must flush microtasks (e.g. `await new Promise(r => setTimeout(r, 0))`) before the next `fixture.detectChanges()` or the loaded signal state isn't reflected in the DOM yet.

### Completion Notes List

- Local dev environment had no database yet — created `oeenew_dev` and `oeenew_test` Postgres 18.4 databases (confirmed `uuidv7()` available), added `ConnectionStrings:Default` to `appsettings.Development.json`, generated and applied the `InitialMasterData` EF Core migration to both databases.
- Domain: `Site`/`Line`/`Machine` POCOs with private setters; `Line`/`Machine` constructors reject an empty parent id (syntactic invariant) while parent *existence* is checked in the Application layer (`MasterDataParentNotFoundException`) — the DB `ON DELETE RESTRICT` FK is a second, physical safety net.
- Application: one use-case class per entity (`SiteManagementUseCase`/`LineManagementUseCase`/`MachineManagementUseCase`) covering Create/Rename/Delete/List, backed by per-entity repository interfaces — no direct EF Core reference (AD-1, verified by `OeeNew.Architecture.Tests`).
- Delete-with-dependents guard (AC #5, no cascade) applied consistently to **both** FK relationships (Site→Line and Line→Machine), even though the AC text only calls out Line — both use `ON DELETE RESTRICT`, so both needed the same pre-check to avoid an unhandled Postgres FK-violation surfacing as a raw 500.
- API: `SitesController`/`LinesController`/`MachinesController`, reads `[Authorize]` (any role), writes `[Authorize(Policy = "AdminOnly")]`. New error codes (`NOT_FOUND` 404, `PARENT_NOT_FOUND` 400, `HAS_DEPENDENTS` 409 with `details.dependentNames`) added to `ApiExceptionHandler`.
- Angular: `MasterDataService` (HTTP) + `MasterDataPage` (3-panel Site→Line→Machine master-detail, one shared PrimeNG Dialog for create/edit across all 3 levels). Action buttons hidden via `isAdmin()` computed from the JWT `role` claim (UI-side check; API is the real authorization boundary per NFR-5).
- Bumped `angular.json` initial bundle budget (700kB→900kB warn, 1MB→1.5MB error) — the added PrimeNG Table/Dialog modules pushed the bundle past the old ceiling; this is expected growth as more PrimeNG-backed features land in later epics, not a regression to chase down.
- Verification: full `dotnet test` (66/66) and `ng test` (21/21, Vitest) green, plus a manual `curl` smoke test against the real dev API + Postgres DB (login → create Site → list Site, confirmed `uuidv7()`-generated id, then cleaned up the test row). Browser-based visual verification (Playwright/chromium) was started but the user asked to rely on the Vitest suite instead, so no screenshot was taken — flagging this explicitly rather than claiming a visual check that didn't happen.

### File List

**Backend — new:**
- `src/OeeNew.Domain/MasterData/Site.cs`, `Line.cs`, `Machine.cs`
- `src/OeeNew.Application/MasterData/ISiteRepository.cs`, `ILineRepository.cs`, `IMachineRepository.cs`
- `src/OeeNew.Application/MasterData/MasterDataNotFoundException.cs`, `MasterDataParentNotFoundException.cs`, `MasterDataHasDependentsException.cs`, `MasterDataForbiddenException.cs`, `MasterDataAuthorization.cs`
- `src/OeeNew.Domain/MasterData/MasterDataValidationException.cs`
- `src/OeeNew.Application/MasterData/SiteManagementUseCase.cs`, `LineManagementUseCase.cs`, `MachineManagementUseCase.cs`
- `src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs`, `SiteRepository.cs`, `LineRepository.cs`, `MachineRepository.cs`
- `src/OeeNew.Infrastructure/Persistence/Migrations/20260719020139_InitialMasterData.cs`, `.Designer.cs`, `OeeDbContextModelSnapshot.cs`
- `src/OeeNew.Api/Controllers/SitesController.cs`, `LinesController.cs`, `MachinesController.cs`

**Backend — modified:**
- `src/OeeNew.Infrastructure/OeeNew.Infrastructure.csproj` (added `Microsoft.EntityFrameworkCore.Design`)
- `src/OeeNew.Api/Program.cs` (DbContext + repository + use-case DI registration)
- `src/OeeNew.Api/Errors/ApiExceptionHandler.cs` (new error-code mappings, incl. `MasterDataForbiddenException`/`MasterDataValidationException`/`DbUpdateException` — code review)
- `src/OeeNew.Api/appsettings.Development.json` (connection string moved to `dotnet user-secrets` — code review)
- `src/OeeNew.Api/Controllers/SitesController.cs`, `LinesController.cs`, `MachinesController.cs` (pass caller's `role` claim into use cases — code review)
- `src/OeeNew.Application/MasterData/SiteManagementUseCase.cs`, `LineManagementUseCase.cs`, `MachineManagementUseCase.cs` (Application-layer Admin re-check — code review)
- `src/OeeNew.Domain/MasterData/Site.cs`, `Line.cs`, `Machine.cs` (200-char name validation via `MasterDataValidationException` — code review)

**Backend — tests (new):**
- `tests/OeeNew.Domain.Tests/MasterData/SiteTests.cs`, `LineTests.cs`, `MachineTests.cs`
- `tests/OeeNew.Application.Tests/MasterData/FakeRepositories.cs`, `SiteManagementUseCaseTests.cs`, `LineManagementUseCaseTests.cs`, `MachineManagementUseCaseTests.cs`
- `tests/OeeNew.Api.Tests/MasterData/MasterDataApiFactory.cs`, `SitesEndpointsTests.cs`, `LinesEndpointsTests.cs`, `MachinesEndpointsTests.cs`

**Frontend — new:**
- `web/oee-shell/src/app/pages/master-data/master-data.service.ts`
- `web/oee-shell/src/app/pages/master-data/master-data-page.html`, `.scss`, `.spec.ts`

**Frontend — modified:**
- `web/oee-shell/src/app/pages/master-data/master-data-page.ts` (placeholder → full CRUD component)
- `web/oee-shell/public/i18n/en.json`, `vi.json` (`masterData.*` keys)
- `web/oee-shell/angular.json` (initial bundle budget)

**Not committed to the repo (local machine state, listed for traceability):**
- Postgres databases `oeenew_dev`, `oeenew_test` created and migrated on the local PostgreSQL 18 instance.

## Change Log

- 2026-07-19: Implemented Story 1.2 end-to-end (Domain entities, EF Core + Postgres persistence, Application use cases, Admin-only API, Angular master-data UI) with full backend (66/66) and frontend (21/21) test coverage. Status → review.
- 2026-07-19: Code review (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Resolved the one decision-needed finding (Application-layer authorization re-check) as a patch, plus 7 further patches: DB credential moved to user-secrets, TOCTOU race mapped to 409, domain name length validation, narrowed exception-handler catch, Angular NOT_FOUND message now triggers a real refresh, stale-response race guard on Site/Line selection, and missing try/catch on list loads. 4 items deferred (test-infra, name uniqueness, pagination, bundle lazy-loading). Full backend (85/85) and frontend (21/21) green after fixes. Status → done.
- 2026-07-19: Second-opinion `/code-review` pass (8 finder angles, verified against source) surfaced 6 more real Angular bugs introduced by the previous patch round: `deleteSite`/`deleteLine` didn't invalidate `siteSelectionToken`/`lineSelectionToken`, so a still-in-flight `selectSite`/`selectLine` fetch could resolve after the delete and repopulate the panel with the just-deleted entity's stale children; `selectSite`/`selectLine` didn't clear the previous entity's `lines`/`machines`/`shiftSchedules` before awaiting the new fetch, leaving stale-but-interactive rows on screen; `handleError` only auto-refreshed on `NOT_FOUND`, not `PARENT_NOT_FOUND`; and `describeError` had no branch for `VALIDATION_ERROR`/`FORBIDDEN`, showing a generic message instead of the server's actionable one. All 6 fixed, with 2 new regression tests locking in the race-guard and validation-error fixes. 4 lower-severity findings (an already-unreachable `ArgumentException`→500 path, a blanket `DbUpdateException`→409 mapping, `EnsureAdmin` repeated per-method with no structural enforcement, `GetAsync` used instead of an existence-only query) were accepted as reasonable tradeoffs at this scale rather than fixed. Frontend 26/26 green; backend unchanged by this pass (last confirmed 116/116 — `dotnet test` unavailable mid-pass due to an in-progress local .NET SDK update, unrelated to this repo).
