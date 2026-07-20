---
baseline_commit: 02a9f37398e618878419952bcf67de3b9559f700
---

# Story 1.5: Quản lý mã lý do dừng máy & nhóm tổn thất

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an Admin,
I want to create/edit/delete downtime reason codes and assign each to exactly one loss category,
so that downtime rolls up correctly into the OEE formula.

## Acceptance Criteria

1. **Given** tôi là Admin **When** tạo Reason Code mới **Then** bắt buộc chọn đúng 1 `LossCategory` (AvailabilityLoss|PerformanceLoss|QualityLoss), NOT NULL ở schema DB (AD-5) — không thể để trống kể cả gọi API trực tiếp
2. **Given** Reason Code không còn cần dùng nữa **When** Admin deactivate (thay vì xoá cứng) **Then** Reason Code ẩn khỏi Reason Code Picker của Operator (Epic 2, Story 2.5) nhưng vẫn giữ nguyên trong dữ liệu lịch sử `[ASSUMPTION]`
3. **Given** tôi là Manager/Operator/Viewer **When** gọi API tạo/sửa/xoá Reason Code **Then** bị từ chối (FR-015, NFR-5)

> **Lưu ý phạm vi (quan trọng):** AC "chặn xoá cứng nếu Reason Code đã có DowntimeEvent tham chiếu" **không** thuộc phạm vi story này — bảng `DowntimeEvent` chưa tồn tại (được tạo ở Epic 2, Story 2.5). Hành vi đó đã được thêm làm AC bổ sung tại Story 2.5 (nơi FK thực sự tồn tại và có thể test). Story 1.5 chỉ cần triển khai deactivate (soft-toggle) — xem `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-18.md` mục "Remediation Applied" để biết lý do tách.

## Tasks / Subtasks

- [x] Task 1: Domain entity `ReasonCode` (AC: #1, #2)
  - [x] `OeeNew.Domain`: entity `ReasonCode` (Id: Guid, SiteId: Guid, Name, `LossCategory` enum bắt buộc, `IsActive` bool mặc định true)
  - [x] Domain: không cho tạo `ReasonCode` thiếu `LossCategory` (enum không cho giá trị null/undefined ở tầng ngôn ngữ, nhưng vẫn viết validation rõ ràng thay vì dựa hoàn toàn vào type system)
- [x] Task 2: EF Core + Postgres (AC: #1)
  - [x] Migration bảng `ReasonCode`, `Id UUID PRIMARY KEY DEFAULT uuidv7()` (pattern giống 1.2/1.3), cột `LossCategory` kiểu enum Postgres hoặc `smallint` mapped enum — **`NOT NULL` ở schema**, không phải chỉ validate ở tầng ứng dụng (đây là yêu cầu tường minh của AD-5, không phải tuỳ chọn implementation)
  - [x] Cột `IsActive boolean NOT NULL DEFAULT true`
- [x] Task 3: Application + API (AC: #1, #2, #3)
  - [x] Use case tạo Reason Code: reject nếu thiếu LossCategory (dù DB đã chặn, trả lỗi envelope thân thiện thay vì để lộ lỗi constraint DB thô)
  - [x] Use case "deactivate": set `IsActive = false`, không xoá bản ghi (không có endpoint hard-delete ở story này — nếu Admin gọi xoá, action mặc định là deactivate)
  - [x] Controller `[Authorize(Policy = "AdminOnly")]`
- [x] Task 4: Angular UI (AC: #1, #2, #3)
  - [x] Màn hình trong `web/oee-shell/src/app/master-data`: form tạo Reason Code bắt buộc chọn LossCategory (dropdown 3 giá trị, không có lựa chọn "để trống"); toggle Active/Inactive thay cho nút xoá
- [x] Task 5: Testing (tất cả AC)
  - [x] Integration test: tạo Reason Code thiếu LossCategory → lỗi rõ ràng (không phải raw DB exception); deactivate → `IsActive=false`, không còn bản ghi bị xoá; role≠Admin → 403
  - [x] DB test: thử insert trực tiếp qua SQL thiếu `LossCategory` → constraint chặn (xác nhận AC1 "kể cả gọi API trực tiếp" nghĩa là chặn ở tầng DB, không chỉ tầng ứng dụng)

## Dev Notes

- **AD-5 là trọng tâm:** `LossCategory` NOT NULL **ở schema DB**, không phải chỉ validation code — vì taxonomy tổn thất phải toàn cục và đáng tin cậy cho báo cáo/pie chart (Epic 3, Epic 4) tổng hợp xuyên site. Nếu chỉ validate ở tầng ứng dụng, một bug tương lai có thể tạo ra Reason Code không phân loại được, làm hỏng toàn bộ báo cáo OEE.
- **Forward-dependency đã được xử lý (xem block "Lưu ý phạm vi" trên):** Bản gốc của epics.md từng viết AC "chặn xoá nếu đã có DowntimeEvent tham chiếu" ngay tại story này — điều này **không thể test được** vì DowntimeEvent chưa tồn tại. Implementation Readiness Assessment (2026-07-18) đã phát hiện và tách hành vi này sang Story 2.5. Dev **không cần** tự thêm logic FK-check ở story này.
- **Tái sử dụng pattern:** Giống hệt Story 1.2/1.3 (Domain entity + EF Core uuidv7 + Application use case + AdminOnly policy + error envelope).
- **LossCategory 3 giá trị cố định:** `AvailabilityLoss | PerformanceLoss | QualityLoss` — không được mở rộng thêm giá trị tự do, vì Epic 3 (pie chart) và Epic 4 (báo cáo) code cứng theo đúng 3 giá trị này (palette màu ở DESIGN.md cũng chỉ định nghĩa 3 màu tương ứng).

### Project Structure Notes

- Entity vào `src/OeeNew.Domain/` cùng namespace MasterData.
- UI vào `web/oee-shell/src/app/master-data/`.

### References

- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-5] — LossCategory NOT NULL ở schema
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/DESIGN.md#Colors] — 3 màu loss category cố định, dùng ở Epic 3
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-014] — yêu cầu gốc
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-1] — Story 1.5 đầy đủ AC (đã sửa sau Implementation Readiness)
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-18.md#Remediation-Applied] — lý do tách AC "chặn xoá cứng" sang Story 2.5
- [Source: _bmad-output/implementation-artifacts/1-2-site-line-machine-management.md] — pattern CRUD/uuidv7/AdminOnly tái sử dụng

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia — BMad dev agent)

### Debug Log References

- `dotnet test` (full solution): 173/173 passed (Domain.Tests 55, Application.Tests 75, Architecture.Tests 2, Api.Tests 41 — the Api.Tests run required the `AddReasonCode` migration applied to the local `oeenew_test` Postgres via `dotnet ef database update`).
- `npx ng test` (oee-shell): 37/37 passed across 7 spec files.
- `npx ng build` (production config): succeeded within the existing 1.3MB bundle budget (no new PrimeNG modules needed — reused `p-select`/`p-table`/`p-dialog` already imported for Story 1.4).

### Completion Notes List

- **AD-5 enforced at both layers, per the story's explicit split:** `LossCategory` is `smallint NOT NULL` in the `ReasonCode` migration (verified by `RawSqlInsert_WithoutLossCategory_IsRejectedByDbConstraint`, which bypasses the API/EF entirely via `ExecuteSqlInterpolatedAsync` and asserts a `Npgsql.PostgresException`) — this is the "even a raw SQL/API bypass can't leave it blank" guarantee AC #1 asks for. Separately, `CreateReasonCodeRequest.LossCategory` is typed `LossCategory?` (nullable) so a raw API call omitting the field binds to `null` instead of silently defaulting to `AvailabilityLoss` (enum index 0) — `ReasonCodeManagementUseCase.CreateAsync` rejects `null` with a clean `VALIDATION_ERROR` before it ever reaches the DB.
- **No hard-delete endpoint exists**, only `PUT /api/master-data/reason-codes/{id}/deactivate` — matches the story's explicit scope note that FK-based hard-delete blocking belongs to Story 2.5 once `DowntimeEvent` exists. The Angular UI accordingly has a "Deactivate" action instead of a delete button, and the button hides itself once a reason code is already inactive (no re-activate flow was requested by any AC/task).
- **Reused the exact Story 1.2/1.3 CRUD pattern** (Domain entity + EF Core uuidv7 + Application use case with `MasterDataAuthorization.EnsureAdmin` + `[Authorize(Policy = "AdminOnly")]` + error envelope) — no new architectural seam needed, unlike Story 1.4.
- **Angular UI** added a fourth panel (Reason Codes) to the existing Site-scoped section of `master-data-page`, alongside Shift Schedules — loaded together via `Promise.all` in `selectSite`. The LossCategory `p-select` always has a pre-selected value (`AvailabilityLoss` by default when the create dialog opens), so there is no "blank" option for a user to pick, matching Task 4's requirement.

### File List

**Backend — new:**
- `src/OeeNew.Domain/MasterData/LossCategory.cs`, `ReasonCode.cs`
- `src/OeeNew.Application/MasterData/IReasonCodeRepository.cs`, `ReasonCodeManagementUseCase.cs`
- `src/OeeNew.Infrastructure/Persistence/ReasonCodeRepository.cs`
- `src/OeeNew.Infrastructure/Persistence/Migrations/20260720135632_AddReasonCode.cs`, `.Designer.cs` (+ updated `OeeDbContextModelSnapshot.cs`)
- `src/OeeNew.Api/Controllers/ReasonCodesController.cs`
- `tests/OeeNew.Domain.Tests/MasterData/ReasonCodeTests.cs`
- `tests/OeeNew.Application.Tests/MasterData/ReasonCodeManagementUseCaseTests.cs` (+ `FakeReasonCodeRepository` added to `FakeRepositories.cs`)
- `tests/OeeNew.Api.Tests/MasterData/ReasonCodesEndpointsTests.cs`

**Backend — modified:**
- `src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs` (`ReasonCode` DbSet + mapping)
- `src/OeeNew.Api/Program.cs` (`IReasonCodeRepository`/`ReasonCodeManagementUseCase` DI registration)

**Frontend — modified:**
- `web/oee-shell/src/app/pages/master-data/master-data.service.ts` (`ReasonCodeDto`/`LossCategoryValue` + `listReasonCodes`/`createReasonCode`/`deactivateReasonCode`)
- `web/oee-shell/src/app/pages/master-data/master-data-page.ts`, `.html` (Reason Codes panel + create dialog: mandatory LossCategory select, Deactivate action)
- `web/oee-shell/src/app/pages/master-data/master-data-page.spec.ts` (2 new tests; 4 existing `selectSite` call sites updated to also flush the new `/reason-codes` request)
- `web/oee-shell/public/i18n/en.json`, `vi.json` (`masterData.reasonCodes`, `masterData.lossCategory.*`, `masterData.active`/`inactive`/`deactivate`)

**Not committed to the repo (local machine state, listed for traceability):**
- `AddReasonCode` migration applied to local Postgres database `oeenew_test` (not yet applied to `oeenew_dev`).

## Change Log

- 2026-07-20: Initial implementation — `ReasonCode` entity (Site-scoped, `LossCategory` NOT NULL at both DB and Application layers, `IsActive` soft-toggle) for all 3 ACs. 173/173 backend tests passing (55 Domain, 75 Application, 2 Architecture, 41 Api), 37/37 frontend tests passing. Status → review.
