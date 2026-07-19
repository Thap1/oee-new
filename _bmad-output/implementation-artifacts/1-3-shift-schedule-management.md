---
baseline_commit: acc0da8b12530ade9a63e789fea63451e34c6714
---

# Story 1.3: Quản lý ca làm việc (Shift Schedule)

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin,
I want to define shift schedules per Site/Line,
so that reports and dashboards group data by đúng ca.

## Acceptance Criteria

1. **Given** tôi là Admin **When** tạo ca (tên + giờ bắt đầu/kết thúc) cho một Site (tuỳ chọn scope theo Line) **Then** ca được lưu và sẵn sàng cho báo cáo (Epic 4)
2. **Given** hai ca cùng Site/Line có khung giờ chồng lấn **When** lưu ca thứ hai **Then** bị từ chối với lỗi rõ ràng **And** `[ASSUMPTION]` tránh đếm trùng thời gian sản xuất
3. **Given** tôi là Manager/Operator/Viewer **When** gọi API tạo/sửa/xoá ca **Then** bị từ chối (FR-015, NFR-5)

## Tasks / Subtasks

- [x] Task 1: Domain entity `ShiftSchedule` (AC: #1, #2)
  - [x] `OeeNew.Domain`: entity `ShiftSchedule` (Id: Guid, SiteId: Guid bắt buộc, LineId: Guid? tuỳ chọn, Name, StartTime, EndTime — kiểu time-of-day, không phải datetime tuyệt đối vì ca lặp lại hàng ngày)
  - [x] Domain logic kiểm tra chồng lấn giờ: so 2 khoảng [StartTime, EndTime) cùng Site (+Line nếu có scope) — đặt trong Domain để test độc lập không cần DB (AD-1)
- [x] Task 2: EF Core + Postgres (AC: #1)
  - [x] Migration bảng `ShiftSchedule`, `Id UUID PRIMARY KEY DEFAULT uuidv7()` (AD-6, giống pattern Story 1.2), FK `SiteId` bắt buộc, `LineId` nullable
- [x] Task 3: Application + API (AC: #1, #2, #3)
  - [x] Use case tạo ca: gọi domain overlap-check trước khi persist; nếu chồng lấn → trả lỗi qua error envelope (Story 1.1) với `code` riêng (vd. `SHIFT_OVERLAP`)
  - [x] Controller `[Authorize(Policy = "AdminOnly")]` cho endpoint ghi, giống pattern Story 1.2
- [x] Task 4: Angular UI (AC: #1, #2, #3)
  - [x] Màn hình trong `web/oee-shell/src/app/master-data` (cùng module với Site/Line/Machine — không tạo module riêng, đây là sub-tab/route con của Master Data theo IA đã chốt ở EXPERIENCE.md)
  - [x] Form chọn Site (bắt buộc) + Line (tuỳ chọn) + time picker start/end; hiển thị lỗi overlap rõ ràng nếu API trả `SHIFT_OVERLAP`
- [x] Task 5: Testing (tất cả AC)
  - [x] Unit test Domain: overlap-check với các case biên (liền kề nhưng không chồng, chồng một phần, bao trùm hoàn toàn, ca qua nửa đêm nếu áp dụng)
  - [x] Integration test API: tạo ca chồng lấn → lỗi `SHIFT_OVERLAP`; role≠Admin → 403

## Dev Notes

- **Tái sử dụng pattern Story 1.2:** Cùng cách làm — Domain entity thuần, EF Core migration với `uuidv7()`, Application use case, Controller `AdminOnly` policy, error envelope chuẩn. Không phát minh lại cấu trúc CRUD, chỉ áp dụng cùng khuôn mẫu cho entity mới.
- **Ca qua nửa đêm:** Nếu shift schedule có ca đêm (vd 22:00–06:00, EndTime < StartTime), thuật toán overlap-check phải xử lý wrap-around — đây là chi tiết dev cần tự quyết định khi implement vì PRD/Architecture không nói rõ, nhưng cần lưu ý để tránh bug thầm lặng khi Reports (Epic 4) tính theo ca.
- **Không có FK ngược từ DowntimeEvent/ProductionCount ở story này** — ShiftSchedule chỉ được tạo ra, việc dùng nó để group báo cáo là công việc của Epic 4 (Story 4.1), không phải phạm vi story này.
- **Kế thừa từ Story 1.1/1.2:** Auth, error envelope, Angular shell, Master Data module đã tồn tại — chỉ thêm entity/route mới.

### Project Structure Notes

- Entity vào `src/OeeNew.Domain/` cùng namespace MasterData như Site/Line/Machine.
- UI: thêm route/tab trong `web/oee-shell/src/app/master-data/` đã có từ Story 1.2, không tạo thư mục module mới.

### References

- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-012] — yêu cầu gốc
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Core-Entity-ERD] — `SITE ||--o{ SHIFT_SCHEDULE : defines`
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-1] — Story 1.3 đầy đủ AC
- [Source: _bmad-output/implementation-artifacts/1-2-site-line-machine-management.md] — pattern CRUD/uuidv7/AdminOnly policy tái sử dụng nguyên vẹn

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- A `dotnet ef` migration/test run was initially blocked by MSB3026/MSB3027 file-lock errors — a leftover `OeeNew.Api` process from a Visual Studio debug session (PID 15280) held the build output DLLs open. Stopped by the user; builds/tests proceeded normally after.

### Completion Notes List

- Domain: `ShiftSchedule` follows the Story 1.2 entity pattern (private setters, `MasterDataValidationException` for name/time validation) plus an `OverlapsWith` method — the overlap-check compares two shifts only when they share the exact same `(SiteId, LineId)` scope (a site-wide shift and a line-scoped shift under the same site do **not** conflict with each other, since AC #2 reads "cùng Site/Line" as an exact scope match, not a hierarchical one). Overnight shifts (`EndTime < StartTime`, e.g. 22:00–06:00) are handled by splitting each shift into its wrapped-midnight minute segments before comparing — a plain start/end comparison would miss overlaps that cross midnight.
- Application: `ShiftScheduleManagementUseCase` reuses `MasterDataAuthorization.EnsureAdmin` (introduced in the Story 1.2 code review) for the Admin re-check, verifies the parent Site exists and — if a Line scope is given — that the Line exists **and belongs to that Site**, then runs the overlap-check against all existing shifts in the same Site before persisting. `RescheduleAsync` re-runs the overlap-check excluding the shift's own id so a no-op reschedule (same times) doesn't conflict with itself.
- Infrastructure: `ShiftSchedule` mapped with a Postgres `time` column (not `timestamp` — the shift recurs daily) via EF Core's native `TimeOnly` support; `SiteId`/`LineId` FKs use `ON DELETE RESTRICT` (no cascade, consistent with Story 1.2). Migration `AddShiftSchedule` generated and applied to both local `oeenew_dev` and `oeenew_test` Postgres databases.
- API: `ShiftSchedulesController` mirrors the Story 1.2 controller shape (reads `[Authorize]`, writes `[Authorize(Policy = "AdminOnly")]`, caller role passed into the use case). New `SHIFT_OVERLAP` (409) error code added to `ApiExceptionHandler`.
- Angular: added a fourth "Shift Schedules" panel to the existing `MasterDataPage` (no new route/module, per Dev Notes) with its own dialog (Name, Line select via PrimeNG `p-select` — "All Lines" option for site-wide, start/end `<input type="time">`). Kept as a separate dialog/state from the shared Site/Line/Machine one since the field shape differs (extra Line + two time inputs). `<input type="time">` uses `HH:mm`; the API's `TimeOnly` serializes as `HH:mm:ss` — converted at the service-call boundary (`toApiTime`/`toInputTime`).
- Bumped the Angular initial bundle warning budget again (900kB→1.2MB) for the added `p-select` module — same expected-growth pattern flagged (and left as-is) in the Story 1.2 review; error budget (1.5MB) unchanged and not approached.
- Verification: full `dotnet test` (116/116: Domain 35, Application 51, Architecture 2, Api 28) and `ng test` (24/24, Vitest) green.

### File List

**Backend — new:**
- `src/OeeNew.Domain/MasterData/ShiftSchedule.cs`
- `src/OeeNew.Application/MasterData/IShiftScheduleRepository.cs`, `ShiftOverlapException.cs`, `ShiftScheduleManagementUseCase.cs`
- `src/OeeNew.Infrastructure/Persistence/ShiftScheduleRepository.cs`
- `src/OeeNew.Infrastructure/Persistence/Migrations/20260719025509_AddShiftSchedule.cs`, `.Designer.cs` (+ updated `OeeDbContextModelSnapshot.cs`)
- `src/OeeNew.Api/Controllers/ShiftSchedulesController.cs`

**Backend — modified:**
- `src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs` (`ShiftSchedule` DbSet + mapping)
- `src/OeeNew.Api/Program.cs` (repository + use-case DI registration)
- `src/OeeNew.Api/Errors/ApiExceptionHandler.cs` (`SHIFT_OVERLAP` 409 mapping)

**Backend — tests (new):**
- `tests/OeeNew.Domain.Tests/MasterData/ShiftScheduleTests.cs`
- `tests/OeeNew.Application.Tests/MasterData/ShiftScheduleManagementUseCaseTests.cs`
- `tests/OeeNew.Api.Tests/MasterData/ShiftSchedulesEndpointsTests.cs`

**Backend — tests (modified):**
- `tests/OeeNew.Application.Tests/MasterData/FakeRepositories.cs` (`FakeShiftScheduleRepository`)

**Frontend — modified:**
- `web/oee-shell/src/app/pages/master-data/master-data.service.ts` (`ShiftScheduleDto` + CRUD methods)
- `web/oee-shell/src/app/pages/master-data/master-data-page.ts`, `.html`, `.scss` (Shift Schedules panel + dialog)
- `web/oee-shell/src/app/pages/master-data/master-data-page.spec.ts` (3 new tests)
- `web/oee-shell/public/i18n/en.json`, `vi.json` (`masterData.shiftSchedules` etc.)
- `web/oee-shell/angular.json` (initial bundle budget 900kB→1.2MB)

**Not committed to the repo (local machine state, listed for traceability):**
- `AddShiftSchedule` migration applied to local Postgres databases `oeenew_dev`, `oeenew_test`.

## Change Log

- 2026-07-19: Implemented Story 1.3 end-to-end (Domain `ShiftSchedule` entity with overlap-check, EF Core + Postgres persistence, Application use case with Admin re-check and overlap guard, `SHIFT_OVERLAP` API error, Angular Shift Schedules panel) with full backend (116/116) and frontend (24/24) test coverage. Status → review.
