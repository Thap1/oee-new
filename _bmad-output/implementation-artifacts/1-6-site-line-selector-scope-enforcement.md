# Story 1.6: Site/Line Selector & Thực thi phạm vi truy cập toàn hệ thống

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Manager/Admin with access to more than one Site/Line,
I want a topbar selector that only appears when relevant,
so that I can switch context without clutter for single-site users.

## Acceptance Criteria

1. **Given** tôi chỉ có quyền trên 1 Site **When** bất kỳ màn hình nào tải **Then** selector ẩn hoàn toàn (UX-DR3)
2. **Given** tôi có quyền trên >1 Site **When** mở selector **Then** chỉ thấy Site/Line trong claim `siteId`/`lineIds` của JWT — không bao giờ thấy site ngoài phạm vi
3. **Given** tôi đổi Site/Line trong selector **When** selection thay đổi **Then** mọi danh sách master-data (1.2-1.5) và màn hình dashboard/báo cáo sau này lọc lại theo scope mới
4. **Given** tôi gọi thẳng API master-data với `siteId`/`lineId` ngoài claim JWT (bỏ qua UI selector) **When** request được gửi **Then** API vẫn từ chối (FR-015, NFR-5) — xác nhận enforcement ở server, không chỉ ẩn/hiện UI **And** cùng nguyên tắc này áp dụng cho API dashboard/báo cáo khi các API đó được xây ở Epic 2/4 (xem Story 2.4 AC3, Story 4.2 AC3) — không lặp lại kiểm thử ở đây

## Tasks / Subtasks

- [ ] Task 1: Topbar Site/Line selector component (AC: #1, #2)
  - [ ] Thêm vào topbar shell đã dựng ở Story 1.1: đọc claim `siteId`/`lineIds` từ JWT hiện tại (decode client-side chỉ để hiển thị — không dùng để enforce, enforce luôn ở server)
  - [ ] Nếu số Site trong claim = 1 → ẩn hoàn toàn selector (UX-DR3, progressive disclosure)
  - [ ] Nếu > 1 → hiện dropdown, chỉ liệt kê Site/Line có trong claim (không gọi API "lấy tất cả site" rồi filter client-side — phải chỉ nhận về đúng phạm vi từ server ngay từ đầu, tránh rò rỉ thông tin site khác qua response)
- [ ] Task 2: Global scope state (AC: #3)
  - [ ] Angular service/signal lưu Site/Line đang chọn, dùng chung cho mọi màn hình (master-data hiện tại; dashboard/reports sẽ dùng lại ở Epic 2/4 — thiết kế service này đủ tổng quát để tái sử dụng, không gắn cứng vào riêng master-data)
  - [ ] Danh sách Site/Line/Machine/ShiftSchedule/User/ReasonCode (Story 1.2-1.5) refilter khi scope đổi
- [ ] Task 3: Server-side enforcement cho master-data API (AC: #4)
  - [ ] Xác nhận (không phải tạo mới) — các controller Story 1.2-1.5 đã có `[Authorize(Policy = "AdminOnly")]` cho thao tác ghi; task này bổ sung kiểm tra **đọc** (GET/list) cũng phải lọc theo `siteId`/`lineIds` trong JWT của user hiện tại (kể cả Admin xem nhiều site — Admin chỉ thấy site nằm trong scope của chính JWT đó, không phải "thấy tất cả" trừ khi thiết kế Admin = toàn cục theo AD-7)
  - [ ] Viết test xác nhận: request GET master-data với query param site ngoài JWT claim → server tự lọc bỏ hoặc trả 403, không dựa vào client gửi đúng
- [ ] Task 4: Testing (tất cả AC)
  - [ ] Angular test: selector ẩn khi 1 site; hiện đúng danh sách khi >1 site; đổi selection → danh sách master-data refetch với scope mới
  - [ ] Integration test API: GET master-data với site ngoài JWT claim (giả lập bằng cách sửa query param, bỏ qua UI) → không trả dữ liệu ngoài phạm vi

## Dev Notes

- **Đây là story "khoá" nguyên tắc NFR-5 cho toàn bộ Epic 1:** Story 1.2-1.5 tập trung vào thao tác **ghi** (Admin-only). Story này bổ sung enforcement cho thao tác **đọc** theo scope — nguyên tắc chung sẽ được các Epic sau (2, 4) tái sử dụng y hệt cho dashboard/reports, **không lặp lại** thiết kế mới.
- **Phạm vi đã được thu hẹp sau Implementation Readiness Assessment (2026-07-18):** AC4 bản gốc từng nhắc tới API "dashboard" — dashboard chưa tồn tại (xây ở Epic 2). Đã bỏ để tránh forward-dependency không test được; xem `implementation-readiness-report-2026-07-18.md`. Story 2.4 (AC3) và Story 4.2 (AC3) sẽ tự viết lại đúng cùng nguyên tắc khi các API đó tồn tại — **Dev của Story 1.6 không cần và không nên** cố gắng test enforcement cho dashboard/reports ở đây.
- **Không tin client:** JWT decode ở client chỉ để hiển thị UI đúng ngay lập tức (UX) — nguồn sự thật (source of truth) cho enforcement luôn là server-side check lại JWT của chính request đó, không dựa vào tham số site/line client tự gửi lên.
- **Kế thừa:** Dùng topbar đã dựng ở Story 1.1, danh sách Site/Line/Machine/ShiftSchedule/User/ReasonCode đã có API từ Story 1.2-1.5 — task 3 chỉ bổ sung filter đọc, không viết lại các API này từ đầu.

### Project Structure Notes

- Component selector vào `web/oee-shell/src/app/` (shared/layout, cùng chỗ với topbar Story 1.1).
- Scope state service vào thư mục shared/core của Angular app (không gắn riêng vào `master-data/` vì sẽ tái sử dụng ở dashboard/reports).
- Không có thay đổi source tree backend ngoài việc bổ sung filter đọc vào controller đã có.

### References

- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md#Component-Patterns] — Site/Line Selector, progressive disclosure
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Consistency-Conventions] — Auth: policy-based authorization ở API
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-015] — yêu cầu gốc
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-1] — Story 1.6 đầy đủ AC (đã sửa sau Implementation Readiness)
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-18.md#Remediation-Applied] — lý do bỏ "/dashboard" khỏi AC4
- [Source: _bmad-output/implementation-artifacts/1-1-login-app-shell.md] — topbar shell, JWT claim
- [Source: _bmad-output/implementation-artifacts/1-2-site-line-machine-management.md], [1-3-shift-schedule-management.md], [1-4-user-role-scope-management.md], [1-5-reason-code-management.md] — API đọc cần bổ sung filter scope

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
