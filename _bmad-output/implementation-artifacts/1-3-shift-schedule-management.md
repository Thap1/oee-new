# Story 1.3: Quản lý ca làm việc (Shift Schedule)

Status: ready-for-dev

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

- [ ] Task 1: Domain entity `ShiftSchedule` (AC: #1, #2)
  - [ ] `OeeNew.Domain`: entity `ShiftSchedule` (Id: Guid, SiteId: Guid bắt buộc, LineId: Guid? tuỳ chọn, Name, StartTime, EndTime — kiểu time-of-day, không phải datetime tuyệt đối vì ca lặp lại hàng ngày)
  - [ ] Domain logic kiểm tra chồng lấn giờ: so 2 khoảng [StartTime, EndTime) cùng Site (+Line nếu có scope) — đặt trong Domain để test độc lập không cần DB (AD-1)
- [ ] Task 2: EF Core + Postgres (AC: #1)
  - [ ] Migration bảng `ShiftSchedule`, `Id UUID PRIMARY KEY DEFAULT uuidv7()` (AD-6, giống pattern Story 1.2), FK `SiteId` bắt buộc, `LineId` nullable
- [ ] Task 3: Application + API (AC: #1, #2, #3)
  - [ ] Use case tạo ca: gọi domain overlap-check trước khi persist; nếu chồng lấn → trả lỗi qua error envelope (Story 1.1) với `code` riêng (vd. `SHIFT_OVERLAP`)
  - [ ] Controller `[Authorize(Policy = "AdminOnly")]` cho endpoint ghi, giống pattern Story 1.2
- [ ] Task 4: Angular UI (AC: #1, #2, #3)
  - [ ] Màn hình trong `web/oee-shell/src/app/master-data` (cùng module với Site/Line/Machine — không tạo module riêng, đây là sub-tab/route con của Master Data theo IA đã chốt ở EXPERIENCE.md)
  - [ ] Form chọn Site (bắt buộc) + Line (tuỳ chọn) + time picker start/end; hiển thị lỗi overlap rõ ràng nếu API trả `SHIFT_OVERLAP`
- [ ] Task 5: Testing (tất cả AC)
  - [ ] Unit test Domain: overlap-check với các case biên (liền kề nhưng không chồng, chồng một phần, bao trùm hoàn toàn, ca qua nửa đêm nếu áp dụng)
  - [ ] Integration test API: tạo ca chồng lấn → lỗi `SHIFT_OVERLAP`; role≠Admin → 403

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

### Debug Log References

### Completion Notes List

### File List
