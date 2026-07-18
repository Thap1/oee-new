# Story 1.4: Quản lý người dùng, vai trò & phạm vi site/line

Status: ready-for-dev

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

- [ ] Task 1: Domain entity `User` + role-scoping (AC: #1, #3)
  - [ ] `OeeNew.Domain`: entity `User` (Id: Guid, Role enum Admin|Manager|Operator|Viewer), quan hệ scoping tới Site/Line (`UserSiteAccess`/`UserLineAccess` hoặc tương đương — Admin không cần bản ghi scoping vì toàn cục)
  - [ ] Ràng buộc domain: Admin không được gán site/line cụ thể (role-scoping chỉ áp dụng Manager/Operator/Viewer) — validate ở Domain, không chỉ UI
- [ ] Task 2: Split trách nhiệm site vs trung tâm (AC: #1, #2) — điểm kỹ thuật quan trọng nhất của story này
  - [ ] **Role-scoping (site/line assignment)** ghi tại site (`OeeNew.Infrastructure` của site instance, theo AD-4) — Admin thao tác tại chính site đó
  - [ ] **Credential đăng nhập** (username/password hash hoặc invite, token issuance) do **Identity Provider trung tâm** tạo (AD-7) — site gọi API trung tâm 1 lần khi tạo user mới để provision credential; nếu trung tâm không tới được lúc đó, tạo user thất bại kèm lỗi rõ ràng (không tạo user "nửa vời" thiếu credential)
  - [ ] Sau khi credential đã tạo, user login được ngay cả khi site offline — vì login xác thực tại trung tâm (issue JWT), còn role-scoping (claim `siteId`/`lineIds`) lấy từ dữ liệu đã đồng bộ trước đó theo AD-4/Sync (Epic 5) — với site đầu tiên (trước khi Sync/Epic 5 tồn tại), claim scoping có thể lấy trực tiếp nếu Identity Provider trung tâm đọc được bảng site-local qua kết nối tại thời điểm login/issue-token; nếu chưa có cơ chế Sync, đây là điểm cần Dev quyết định implementation cụ thể (vd Identity Provider gọi API site để lấy scoping tại thời điểm cấp token, hoặc cache) — ghi rõ giả định đã chọn vào Completion Notes
- [ ] Task 3: Application + API (AC: #1, #2, #3, #4)
  - [ ] Use case tạo user tại site: (a) lưu role-scoping local, (b) gọi Identity Provider trung tâm provision credential, (c) rollback role-scoping nếu bước (b) thất bại (tránh dữ liệu mồ côi)
  - [ ] Controller `[Authorize(Policy = "AdminOnly")]` cho tạo/sửa user + đổi role (AC4)
- [ ] Task 4: Angular UI (AC: #1, #3, #4)
  - [ ] Màn hình trong `web/oee-shell/src/app/master-data`: form tạo user — chọn Role; nếu Role≠Admin, hiện multi-select Site/Line (dùng danh sách đã tạo ở Story 1.2); nếu Role=Admin, ẩn phần chọn site/line (không áp dụng)
- [ ] Task 5: Testing (tất cả AC)
  - [ ] Unit test Domain: Admin không nhận role-scoping; Operator bắt buộc có ít nhất 1 Line
  - [ ] Integration test: tạo user Operator → JWT (giả lập login) chứa đúng siteId/lineIds; tạo user khi trung tâm không tới được → lỗi rõ ràng, không có bản ghi role-scoping mồ côi; role≠Admin gọi API → 403

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

### Debug Log References

### Completion Notes List

### File List
