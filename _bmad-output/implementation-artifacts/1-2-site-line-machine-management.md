# Story 1.2: Quản lý danh mục Site/Line/Machine

Status: ready-for-dev

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

- [ ] Task 1: Domain entities (AC: #1, #2, #3)
  - [ ] `OeeNew.Domain`: entity `Site`, `Line`, `Machine` — khoá chính `Guid`, không dependency EF Core trong entity thuần domain nếu theo POCO pattern
  - [ ] Quan hệ: `Line.SiteId` (FK bắt buộc), `Machine.LineId` (FK bắt buộc) — khớp ERD Architecture Spine (Site ||--o{ LINE, LINE ||--o{ MACHINE)
- [ ] Task 2: EF Core + Postgres (AC: #1, #2, #3, #5)
  - [ ] `OeeNew.Infrastructure`: `DbContext`, migration tạo bảng `Site`/`Line`/`Machine`
  - [ ] Cột `Id uuid PRIMARY KEY DEFAULT uuidv7()` — dùng hàm gốc PostgreSQL 18, **không** dùng `Guid.CreateVersion7()` phía .NET (AD-6 — tránh lỗi thứ tự byte/không đơn điệu đã ghi nhận trên .NET runtime)
  - [ ] FK `Line.SiteId`, `Machine.LineId` với `ON DELETE RESTRICT` (không cascade — khớp AC5 `[ASSUMPTION]`)
- [ ] Task 3: Application use case + API (AC: #1, #2, #3, #4, #5)
  - [ ] Use case tạo/sửa/xoá Site/Line/Machine tại `OeeNew.Application`, gọi qua interface, không EF Core trực tiếp
  - [ ] Controller tại `OeeNew.Api`: policy-based authorization yêu cầu `role=Admin` cho mọi endpoint ghi (401/403 map về error envelope đã thiết lập ở Story 1.1)
  - [ ] Xoá Line: kiểm tra tồn tại Machine con trước, nếu có → trả lỗi envelope kèm `details` liệt kê Machine phụ thuộc (AC5)
- [ ] Task 4: Angular UI (AC: #1, #2, #3, #4)
  - [ ] Màn hình `web/oee-shell/src/app/master-data` (thư mục đã scaffold ở Story 1.1): danh sách + form Site/Line/Machine dùng PrimeNG DataTable/Dialog (kế thừa nguyên component Sakai, không custom thêm ngoài 3 biến thể đã chốt ở DESIGN.md)
  - [ ] Ẩn hoàn toàn action tạo/sửa/xoá nếu role hiện tại ≠ Admin (double-check: UI ẩn + API chặn, không chỉ 1 lớp)
- [ ] Task 5: Testing (tất cả AC)
  - [ ] Unit test Domain: validate quan hệ Site>Line>Machine, không tạo Line thiếu Site cha
  - [ ] Integration test API: tạo Site → Guid hợp lệ; xoá Line còn Machine → 4xx với danh sách Machine; role≠Admin gọi API ghi → 403
  - [ ] Angular: component test ẩn nút thao tác khi role≠Admin

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

### Debug Log References

### Completion Notes List

### File List
