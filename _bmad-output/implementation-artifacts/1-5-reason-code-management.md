# Story 1.5: Quản lý mã lý do dừng máy & nhóm tổn thất

Status: ready-for-dev

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

- [ ] Task 1: Domain entity `ReasonCode` (AC: #1, #2)
  - [ ] `OeeNew.Domain`: entity `ReasonCode` (Id: Guid, SiteId: Guid, Name, `LossCategory` enum bắt buộc, `IsActive` bool mặc định true)
  - [ ] Domain: không cho tạo `ReasonCode` thiếu `LossCategory` (enum không cho giá trị null/undefined ở tầng ngôn ngữ, nhưng vẫn viết validation rõ ràng thay vì dựa hoàn toàn vào type system)
- [ ] Task 2: EF Core + Postgres (AC: #1)
  - [ ] Migration bảng `ReasonCode`, `Id UUID PRIMARY KEY DEFAULT uuidv7()` (pattern giống 1.2/1.3), cột `LossCategory` kiểu enum Postgres hoặc `smallint` mapped enum — **`NOT NULL` ở schema**, không phải chỉ validate ở tầng ứng dụng (đây là yêu cầu tường minh của AD-5, không phải tuỳ chọn implementation)
  - [ ] Cột `IsActive boolean NOT NULL DEFAULT true`
- [ ] Task 3: Application + API (AC: #1, #2, #3)
  - [ ] Use case tạo Reason Code: reject nếu thiếu LossCategory (dù DB đã chặn, trả lỗi envelope thân thiện thay vì để lộ lỗi constraint DB thô)
  - [ ] Use case "deactivate": set `IsActive = false`, không xoá bản ghi (không có endpoint hard-delete ở story này — nếu Admin gọi xoá, action mặc định là deactivate)
  - [ ] Controller `[Authorize(Policy = "AdminOnly")]`
- [ ] Task 4: Angular UI (AC: #1, #2, #3)
  - [ ] Màn hình trong `web/oee-shell/src/app/master-data`: form tạo Reason Code bắt buộc chọn LossCategory (dropdown 3 giá trị, không có lựa chọn "để trống"); toggle Active/Inactive thay cho nút xoá
- [ ] Task 5: Testing (tất cả AC)
  - [ ] Integration test: tạo Reason Code thiếu LossCategory → lỗi rõ ràng (không phải raw DB exception); deactivate → `IsActive=false`, không còn bản ghi bị xoá; role≠Admin → 403
  - [ ] DB test: thử insert trực tiếp qua SQL thiếu `LossCategory` → constraint chặn (xác nhận AC1 "kể cả gọi API trực tiếp" nghĩa là chặn ở tầng DB, không chỉ tầng ứng dụng)

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

### Debug Log References

### Completion Notes List

### File List
