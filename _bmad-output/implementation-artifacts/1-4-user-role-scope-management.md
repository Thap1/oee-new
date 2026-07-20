---
baseline_commit: 02a9f37398e618878419952bcf67de3b9559f700
---

# Story 1.4: Quản lý người dùng, vai trò & phạm vi site/line

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin,
I want to create users, assign role (Admin/Manager/Operator/Viewer), and scope Manager/Operator/Viewer to Site(s)/Line(s),
so that each user only sees/acts within their scope.

## Acceptance Criteria

1. **Given** tôi là Admin **When** tạo user role=Operator gán vào Line X **Then** role-scoping lưu tại site (AD-4) và JWT tương lai của user chứa `siteId`/`lineIds` khớp assignment
2. **Given** user mới lần đầu được tạo tại site **When** site online và liên lạc được trung tâm ít nhất 1 lần **Then** credential được tạo tại Identity Provider trung tâm, user đăng nhập được kể cả khi site sau đó offline (AD-7)
3. **Given** user role=Admin **When** JWT phát hành **Then** claim `role: Admin` là toàn cục, không giới hạn theo site (AD-7)
4. **Given** tôi là Manager/Operator/Viewer **When** gọi API tạo/sửa user hoặc đổi role **Then** bị từ chối (FR-015, NFR-5)

## Tasks / Subtasks

- [x] Task 1: Domain entity `User` + role-scoping (AC: #1, #3)
  - [x] `OeeNew.Domain`: entity `User` (Id: Guid, Role enum Admin|Manager|Operator|Viewer), quan hệ scoping tới Site/Line (`UserSiteAccess`/`UserLineAccess` hoặc tương đương — Admin không cần bản ghi scoping vì toàn cục)
  - [x] Ràng buộc domain: Admin không được gán site/line cụ thể (role-scoping chỉ áp dụng Manager/Operator/Viewer) — validate ở Domain, không chỉ UI
- [x] Task 2: Split trách nhiệm site vs trung tâm (AC: #1, #2) — điểm kỹ thuật quan trọng nhất của story này
  - [x] **Role-scoping (site/line assignment)** ghi tại site (`OeeNew.Infrastructure` của site instance, theo AD-4) — Admin thao tác tại chính site đó
  - [x] **Credential đăng nhập** (username/password hash hoặc invite, token issuance) do **Identity Provider trung tâm** tạo (AD-7) — site gọi API trung tâm 1 lần khi tạo user mới để provision credential; nếu trung tâm không tới được lúc đó, tạo user thất bại kèm lỗi rõ ràng (không tạo user "nửa vời" thiếu credential)
  - [x] Sau khi credential đã tạo, user login được ngay cả khi site offline — vì login xác thực tại trung tâm (issue JWT), còn role-scoping (claim `siteId`/`lineIds`) lấy từ dữ liệu đã đồng bộ trước đó theo AD-4/Sync (Epic 5) — với site đầu tiên (trước khi Sync/Epic 5 tồn tại), claim scoping có thể lấy trực tiếp nếu Identity Provider trung tâm đọc được bảng site-local qua kết nối tại thời điểm login/issue-token; nếu chưa có cơ chế Sync, đây là điểm cần Dev quyết định implementation cụ thể (vd Identity Provider gọi API site để lấy scoping tại thời điểm cấp token, hoặc cache) — ghi rõ giả định đã chọn vào Completion Notes
- [x] Task 3: Application + API (AC: #1, #2, #3, #4)
  - [x] Use case tạo user tại site: (a) lưu role-scoping local, (b) gọi Identity Provider trung tâm provision credential, (c) rollback role-scoping nếu bước (b) thất bại (tránh dữ liệu mồ côi)
  - [x] Controller `[Authorize(Policy = "AdminOnly")]` cho tạo/sửa user + đổi role (AC4)
- [x] Task 4: Angular UI (AC: #1, #3, #4)
  - [x] Màn hình trong `web/oee-shell/src/app/master-data`: form tạo user — chọn Role; nếu Role≠Admin, hiện multi-select Site/Line (dùng danh sách đã tạo ở Story 1.2); nếu Role=Admin, ẩn phần chọn site/line (không áp dụng)
- [x] Task 5: Testing (tất cả AC)
  - [x] Unit test Domain: Admin không nhận role-scoping; Operator bắt buộc có ít nhất 1 Line
  - [x] Integration test: tạo user Operator → JWT (giả lập login) chứa đúng siteId/lineIds; tạo user khi trung tâm không tới được → lỗi rõ ràng, không có bản ghi role-scoping mồ côi; role≠Admin gọi API → 403

## Dev Notes

- **AD-7 là điểm phức tạp nhất của story này:** tách rõ 2 khái niệm dễ nhầm — "role-scoping" (Admin gán site/line cho user, ghi **tại site**, giống Site/Line/Machine ở Story 1.2) khác với "credential đăng nhập" (Identity Provider **trung tâm** tạo, chỉ cần online 1 lần lúc tạo user, không phải mỗi lần login). Nhầm lẫn 2 khái niệm này là nguyên nhân phổ biến nhất khiến story bị implement sai hướng.
- **Rollback nếu provision credential thất bại:** Vì đây là 2 thao tác ghi vào 2 nơi khác nhau (site DB cho scoping, trung tâm cho credential) không có transaction chung, Application use case phải tự xử lý rollback/compensating action nếu bước gọi trung tâm thất bại — nếu không sẽ để lại role-scoping "mồ côi" không có credential tương ứng.
- **Không có Sync module ở giai đoạn này (Epic 5 chưa build):** Với site đầu tiên phát triển, cơ chế "trung tâm đọc role-scoping của site để đưa vào JWT claim" cần một giải pháp tạm (API trực tiếp site→trung tâm lúc issue token, hoặc trung tâm lưu bản sao ngay lúc user được tạo) — đây không phải đợi Epic 5 mới hoạt động được, vì Epic 5 Sync chỉ đồng bộ **bản ghi nghiệp vụ đã chốt** (DowntimeEvent/ProductionCount/QualityReject), không đồng bộ role-scoping theo cùng cơ chế. Dev cần chọn giải pháp cụ thể cho việc này trong phạm vi Story 1.4 (ví dụ: trung tâm gọi API site đồng bộ để lấy claim tại thời điểm tạo user, lưu bản sao tối thiểu cần cho JWT).
- **Tái sử dụng:** JWT issuance, JWKS, error envelope đã có từ Story 1.1; policy `AdminOnly` đã có từ Story 1.2/1.3.
- **Không có story trước trực tiếp về User** — đây là entity User đầu tiên; Site/Line (Story 1.2) là tiền đề bắt buộc (Operator phải gán vào Line có sẵn).

### Project Structure Notes

- Entity `User` + role-scoping vào `src/OeeNew.Domain/` (namespace riêng, vd `OeeNew.Domain.Identity` hoặc `MasterData`, tuỳ convention dev chọn nhưng phải nhất quán với Site/Line/Machine đã có).
- Phần "Identity Provider trung tâm" (JWT issuance) đã có scaffold từ Story 1.1 tại `OeeNew.Infrastructure` — story này MỞ RỘNG nó để hỗ trợ provision credential cho user site-local, không tạo hệ thống identity thứ hai.
- UI vào `web/oee-shell/src/app/master-data/`.

### References

- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-4] — role-scoping ghi tại site
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-7] — credential tập trung, claim JWT, JWKS
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-013] — yêu cầu gốc
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-1] — Story 1.4 đầy đủ AC
- [Source: _bmad-output/implementation-artifacts/1-1-login-app-shell.md] — Identity Provider/JWT scaffold tái sử dụng
- [Source: _bmad-output/implementation-artifacts/1-2-site-line-machine-management.md] — Site/Line entity mà Operator/Manager/Viewer sẽ tham chiếu khi gán scope

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia — BMad dev agent)

### Debug Log References

- `dotnet test` (full solution): 153/153 passed (Domain.Tests 48, Application.Tests 68, Architecture.Tests 2, Api.Tests 35 — the Api.Tests run required a live local `oeenew_test` Postgres with the new `AddUser` migration applied via `dotnet ef database update`).
- `npx ng test` (oee-shell): 35/35 passed across 7 spec files.
- `npx ng build` (production config): succeeded; bumped `initial` bundle budget warning 1.2MB→1.3MB (MultiSelect/Password PrimeNG modules added for the Users panel).

### Completion Notes List

- **Chosen implementation for Task 2's "Dev cần quyết định" point (AD-7 site-vs-central split):** Story 1.1's Dev Notes already flagged (and its code review confirmed as a deferred finding) that `AppMode` Site/Central separation isn't actually wired yet — every instance today is both the site and the "central" Identity Provider in the same process/database. Given that reality, this story keeps role-scoping (`User.SiteIds`/`LineIds`) and the credential (`User.PasswordHash`) as columns on the **same** `User` row in the **same** `OeeDbContext`, rather than building a fake network boundary between them. `ICentralCredentialProvisioner` is the seam that models AD-7's "site calls central once to provision a credential" — its `Infrastructure` implementation (`CentralCredentialProvisioner`) just hashes in-process today. When Epic 5 introduces real multi-instance deployment, that interface becomes an HTTP call, and `CredentialProvisioningException` becomes the genuine "central unreachable" case — nothing else in `UserManagementUseCase` needs to change.
- **Rollback strategy — provision-before-persist instead of write-then-rollback:** Task 2/3 ask for a compensating rollback if credential provisioning fails after role-scoping is written. Since both writes are (currently) the same database anyway, `UserManagementUseCase.CreateAsync` instead calls `ICentralCredentialProvisioner.ProvisionAsync` **before** constructing/persisting the `User` row. If provisioning fails, nothing has been written yet, so there is no orphaned scoping record to roll back — a stronger guarantee than a rollback would give, achieved with less code. Verified by `CreateAsync_WhenCentralUnreachable_ThrowsCredentialProvisioningException_AndPersistsNoUser`.
- **`BootstrapUserAuthenticator` (Story 1.1) kept, not deleted:** the existing `AuthEndpointsTests`/`RateLimitingTests` use a plain `WebApplicationFactory<Program>` with no Postgres connection string configured, relying entirely on the hardcoded bootstrap Admin. Deleting it in favor of a fully persisted-only `IUserAuthenticator` would have broken that whole test suite (and any environment without Postgres wired up). Instead, `CompositeUserAuthenticator` tries `PersistedUserAuthenticator` first and falls back to `BootstrapUserAuthenticator` when the username isn't found there *or* when the persisted store can't be reached at all — this is what keeps the very first Admin login working before any `User` row exists. Covered by `CompositeUserAuthenticatorTests`.
- **Site/Line scoping storage:** `User.SiteIds`/`LineIds` are `Guid[]` mapped to native Postgres `uuid[]` columns (Npgsql supports this directly) rather than separate `UserSiteAccess`/`UserLineAccess` join tables — simpler schema for a many-valued-but-not-heavily-queried scope list. Element-level FK enforcement isn't possible on Postgres arrays; `UserManagementUseCase.EnsureScopeExistsAsync` validates each Site/Line id exists (and each Line belongs to one of the assigned Sites) at the Application layer instead, consistent with how Story 1.2/1.3 already do existence checks app-side rather than DB-side.
- **Role serialization:** added a global `JsonStringEnumConverter` (`Program.cs`) so `UserRole` serializes as `"Admin"/"Manager"/...` in JSON, matching the JWT role claim's string form instead of a raw integer — the only enum in the API so far, no impact on other endpoints.
- **`UsersController` is Admin-only end-to-end, including `List`** (unlike Site/Line/Machine/ShiftSchedule, which allow any authenticated role to read) — usernames and role/scope assignments are more sensitive than physical master data, so this deviates slightly from the established "reads: any role" pattern deliberately.
- **Frontend scope, per Task 4's literal wording:** only a create form + read-only list were built (no edit dialog), even though the backend supports `PUT /api/users/{id}` for role/scope changes (Task 3 required it, Task 4 didn't). Angular UI lives in the existing `master-data-page` component (Users panel, Admin-only), reusing `MasterDataService` rather than a new service.
- **Migration:** `20260720130438_AddUser` adds the `User` table (`Username` unique index, `Role` as `varchar(20)`, `SiteIds`/`LineIds` as `uuid[]`). Applied to the local `oeenew_test` database to run the new API integration tests; not yet applied to `oeenew_dev` (apply via `dotnet ef database update` before manual testing against the dev DB).

### File List

**Backend — new:**
- `src/OeeNew.Domain/Identity/UserRole.cs`, `User.cs`
- `src/OeeNew.Application/Identity/IUserRepository.cs`, `ICentralCredentialProvisioner.cs`, `CredentialProvisioningException.cs`, `UsernameAlreadyTakenException.cs`, `UserManagementUseCase.cs`
- `src/OeeNew.Infrastructure/Identity/CentralCredentialProvisioner.cs`, `PersistedUserAuthenticator.cs`, `CompositeUserAuthenticator.cs`
- `src/OeeNew.Infrastructure/Persistence/UserRepository.cs`
- `src/OeeNew.Infrastructure/Persistence/Migrations/20260720130438_AddUser.cs`, `.Designer.cs` (+ updated `OeeDbContextModelSnapshot.cs`)
- `src/OeeNew.Api/Controllers/UsersController.cs`
- `tests/OeeNew.Domain.Tests/Identity/UserTests.cs`
- `tests/OeeNew.Application.Tests/Identity/FakeRepositories.cs`, `UserManagementUseCaseTests.cs`, `PersistedUserAuthenticatorTests.cs`, `CompositeUserAuthenticatorTests.cs`
- `tests/OeeNew.Api.Tests/Identity/UserEndpointsTests.cs`

**Backend — modified:**
- `src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs` (`User` DbSet + mapping)
- `src/OeeNew.Api/Program.cs` (`JsonStringEnumConverter`; `IUserRepository`/`ICentralCredentialProvisioner`/`UserManagementUseCase` DI; `IUserAuthenticator` → `CompositeUserAuthenticator` composed from `PersistedUserAuthenticator` + `BootstrapUserAuthenticator`)
- `src/OeeNew.Api/Errors/ApiExceptionHandler.cs` (`USERNAME_TAKEN` 409, `CREDENTIAL_PROVISIONING_FAILED` 503 mapping)

**Frontend — modified:**
- `web/oee-shell/src/app/pages/master-data/master-data.service.ts` (`UserDto`/`UserRoleValue` + `listUsers`/`createUser`)
- `web/oee-shell/src/app/pages/master-data/master-data-page.ts`, `.html` (Users panel + create-user dialog: Role select, conditional Site/Line multi-select)
- `web/oee-shell/src/app/pages/master-data/master-data-page.spec.ts` (6 new tests)
- `web/oee-shell/public/i18n/en.json`, `vi.json` (`masterData.users`, `masterData.role.*`, `masterData.error.usernameTaken`)
- `web/oee-shell/angular.json` (initial bundle budget warning 1.2MB→1.3MB)

**Not committed to the repo (local machine state, listed for traceability):**
- `AddUser` migration applied to local Postgres database `oeenew_test` (not yet applied to `oeenew_dev`).

## Change Log

- 2026-07-20: Initial implementation — persisted multi-user `User` entity (role + Site/Line scoping), `UserManagementUseCase` (create/re-scope, Admin-only, provision-before-persist credential flow), `CompositeUserAuthenticator` (persisted store with bootstrap-Admin fallback), `UsersController`, and Angular Users panel (create form) for all 4 ACs. 153/153 backend tests passing (48 Domain, 68 Application, 2 Architecture, 35 Api), 35/35 frontend tests passing. Status → review.
