---
stepsCompleted: [1, 2, 3, 4, 5, 6]
includedDocuments:
  prd: "prds/prd-oee-new-2026-07-17/prd.md"
  prd_addendum: "prds/prd-oee-new-2026-07-17/addendum.md"
  architecture: "architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md"
  epics: "epics.md"
  ux_design: "ux-designs/ux-oee-new-2026-07-17/DESIGN.md"
  ux_experience: "ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md"
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-18
**Project:** oee-new

## Document Inventory

**PRD:**
- Whole: `prds/prd-oee-new-2026-07-17/prd.md` + `addendum.md` (status: final)

**Architecture:**
- Whole: `architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md` (status: final)

**Epics & Stories:**
- Whole: `epics.md` (5 epics / 21 stories)

**UX Design:**
- Whole (bmad-ux spine pair): `ux-designs/ux-oee-new-2026-07-17/DESIGN.md` + `EXPERIENCE.md` (status: final)

## Issues Found

- No duplicates (whole vs sharded) detected for any document type.
- No documents missing.

## PRD Analysis

### Functional Requirements

FR-001: Hệ thống phải nhận dữ liệu sản lượng/trạng thái máy từ các máy trong xưởng một cách tự động (không cần nhập tay), theo cấu hình từng máy.
FR-002: Hệ thống phải chuẩn hoá dữ liệu nhận vào bất kể máy sử dụng phương thức kết nối gì. [ASSUMPTION] cho phép nhập bù thủ công cho máy chưa kết nối tự động được.
FR-003: Hệ thống phải phát hiện và cảnh báo (hiển thị) khi một máy ngừng gửi dữ liệu quá thời gian quy định.
FR-004: Operator phải xem được trạng thái/OEE của máy mình phụ trách real-time, cập nhật trong vài giây.
FR-005: Dashboard phải thể hiện trạng thái máy bằng màu sắc rõ ràng, đọc được từ xa.
FR-006: Manager/Viewer phải xem được dashboard tổng hợp nhiều máy/line/site, giới hạn theo phân quyền.
FR-007: Giao diện phải hỗ trợ song ngữ Việt/Anh, chuyển đổi theo lựa chọn người dùng.
FR-008: Khi máy dừng, Operator phải chọn lý do dừng máy từ danh mục chuẩn hoá (không tự do).
FR-009: Mỗi mã lý do dừng máy phải phân nhóm vào đúng 1 trong 3 loại tổn thất OEE (Availability/Performance/Quality).
FR-010: Hệ thống phải ghi nhận số lượng sản phẩm lỗi/phế phẩm cơ bản. [ASSUMPTION] module chất lượng chi tiết ngoài phạm vi MVP.
FR-011: Admin phải quản lý (thêm/sửa/xoá) danh mục Site, Line, Machine theo cấu trúc phân cấp.
FR-012: Admin phải quản lý ca làm việc áp dụng cho từng site/line.
FR-013: Admin phải quản lý người dùng và gán vai trò kèm phạm vi site/line.
FR-014: Admin phải quản lý danh mục mã lý do dừng máy và nhóm tổn thất tương ứng.
FR-015: Hệ thống phải từ chối thao tác ngoài phạm vi site/line phân quyền, ở cả UI lẫn backend.
FR-016: Manager/Viewer phải xem được báo cáo OEE tổng hợp theo ca/ngày/tuần.
FR-017: Báo cáo phải cho phép lọc theo site/line/máy trong phạm vi phân quyền.
FR-018: Báo cáo phải hiển thị nguyên nhân dừng máy chiếm nhiều thời gian nhất trong kỳ.
FR-019: Dashboard phải cung cấp pie chart tỷ lệ phân bổ tổn thất/thời gian. [ASSUMPTION] theo 3 loại tổn thất và/hoặc theo mã lý do — cần xác nhận.
FR-020: Người dùng phải chọn xem pie chart theo Equipment hoặc Production Area, trong phạm vi phân quyền.
FR-021: Người dùng phải xem chi tiết theo ngày (drill-down) của pie chart.

Total FRs: 21

### Non-Functional Requirements

NFR-1 (Real-time): Thay đổi trạng thái máy phải phản ánh lên dashboard trong vòng vài giây, không cần tải lại trang.
NFR-2 (Multi-site): Hệ thống phải hỗ trợ nhiều nhà máy hoạt động đồng thời, với khả năng xem tổng hợp toàn hệ thống cho Admin.
NFR-3 (Triển khai on-premise): Mỗi site có hạ tầng on-premise riêng, hoạt động độc lập; dữ liệu đồng bộ định kỳ về điểm tổng hợp.
NFR-4 (Chịu lỗi mạng xưởng): Ghi nhận lý do dừng máy không phụ thuộc hoàn toàn vào kết nối ra ngoài site.
NFR-5 (Phân quyền): Mọi API/màn hình phải thực thi kiểm soát truy cập theo vai trò và phạm vi site/line.
NFR-6 (Song ngữ): Toàn bộ giao diện phải hỗ trợ tiếng Việt và tiếng Anh.

Total NFRs: 6

### Additional Requirements

- Chỉ số thành công (mục 6 PRD): % thời gian dừng máy có lý do rõ ràng ≥90%/tháng [ASSUMPTION]; giảm thời gian tổng hợp báo cáo cuối ca [ASSUMPTION]; counter-metric: không tăng thời gian nhập liệu của Operator.
- Giả định/Câu hỏi mở (mục 7 PRD): chưa tích hợp giao thức máy thật (OPC-UA/MQTT/Modbus) ở giai đoạn này; timeline & site thí điểm đầu tiên là OPEN QUESTION chưa chốt — không chặn PRD nhưng cần chốt trước Sprint Planning; alert tự động + module chất lượng chi tiết nằm ngoài MVP.
- Addendum kỹ thuật (`addendum.md`): stack đã chọn (Postgres/.NET/Angular+PrimeNG), Ingestion Adapter Pattern, phân cấp Site>Line>Machine, RBAC 4 role, reason code chuẩn hoá, đồng bộ multi-site on-premise — đã được Architecture Spine tiếp nhận đầy đủ (xem AD-1..AD-8).

### PRD Completeness Assessment

PRD ở trạng thái `final`, có 21 FR + 6 NFR rõ ràng, đánh dấu tường minh các `[ASSUMPTION]` và 1 `[OPEN QUESTION]` (timeline/site thí điểm đầu tiên) chưa chốt — không chặn việc lập kế hoạch epic/story nhưng **cần chốt trước Sprint Planning** theo đúng ghi chú trong PRD mục 7. Không phát hiện FR/NFR nào thiếu numbering hoặc mơ hồ đến mức không thể derive story.

## Epic Coverage Validation

### Coverage Matrix

| FR Number | Epic Coverage | Status |
| --- | --- | --- |
| FR-001 | Epic 2, Story 2.1 | ✓ Covered |
| FR-002 | Epic 2, Story 2.1 | ✓ Covered |
| FR-003 | Epic 2, Story 2.3 | ✓ Covered |
| FR-004 | Epic 2, Story 2.2 | ✓ Covered |
| FR-005 | Epic 2, Story 2.2 | ✓ Covered |
| FR-006 | Epic 2, Story 2.4 | ✓ Covered |
| FR-007 | Epic 1, Story 1.1 | ✓ Covered |
| FR-008 | Epic 2, Story 2.5 | ✓ Covered |
| FR-009 | Epic 2, Story 2.5 | ✓ Covered |
| FR-010 | Epic 2, Story 2.6 | ✓ Covered |
| FR-011 | Epic 1, Story 1.2 | ✓ Covered |
| FR-012 | Epic 1, Story 1.3 | ✓ Covered |
| FR-013 | Epic 1, Story 1.4 | ✓ Covered |
| FR-014 | Epic 1, Story 1.5 | ✓ Covered |
| FR-015 | Epic 1 (1.2-1.6) + reused in Epic 2/3/4 ACs | ✓ Covered |
| FR-016 | Epic 4, Story 4.1 | ✓ Covered |
| FR-017 | Epic 4, Story 4.2 | ✓ Covered |
| FR-018 | Epic 4, Story 4.3 | ✓ Covered |
| FR-019 | Epic 3, Story 3.1/3.2 | ✓ Covered |
| FR-020 | Epic 3, Story 3.1 | ✓ Covered |
| FR-021 | Epic 3, Story 3.2 | ✓ Covered |

| NFR Number | Epic Coverage | Status |
| --- | --- | --- |
| NFR-1 | Epic 2, Story 2.2 | ✓ Covered |
| NFR-2 | Epic 1 (nền tảng) + Epic 5, Story 5.2 (đầy đủ) | ✓ Covered |
| NFR-3 | Epic 2, Story 2.1 (site tự vận hành) + Epic 5, Story 5.1 (cơ chế Sync) | ✓ Covered |
| NFR-4 | Epic 2, Story 2.1 | ✓ Covered |
| NFR-5 | Epic 1, Story 1.6 + reused across all write/read ACs | ✓ Covered |
| NFR-6 | Epic 1, Story 1.1 | ✓ Covered |

### Missing Requirements

Không có FR/NFR nào thiếu coverage.

### Coverage Statistics

- Total PRD FRs: 21
- FRs covered in epics: 21
- Coverage percentage: 100%
- Total PRD NFRs: 6
- NFRs covered in epics: 6
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

Found — bmad-ux spine pair `DESIGN.md` + `EXPERIENCE.md` (status: final), explicitly sourced from PRD + Architecture Spine.

### Alignment Issues

Không tìm thấy sai lệch. Chi tiết đối chiếu:

- **UX ↔ PRD:** UJ-1 (Operator ghi downtime) và UJ-2 (Manager xem báo cáo) trong EXPERIENCE.md khớp trực tiếp với PRD mục 3. UJ-3 (Admin xử lý mất tín hiệu) là journey mở rộng hợp lý từ FR-003, không mâu thuẫn PRD. Bảng phân quyền sidebar (Dashboard/Downtime/Reports/Master Data theo vai trò) khớp đúng bảng vai trò PRD mục 2 — Operator không có Reports, chỉ Admin có Master Data.
- **UX ↔ Architecture:** DESIGN.md/EXPERIENCE.md tham chiếu trực tiếp các AD trong Architecture Spine — AD-1 (source tree/layout), AD-4 (Central Master Data read-only + link "Mở tại site X"), AD-5 (nhóm màu loss category riêng biệt màu trạng thái máy), AD-6 (Guid dùng cho filter Equipment/Area), AD-8 (SignalR real-time, không polling). Stack Angular 21 + PrimeNG 21 trong EXPERIENCE.md khớp đúng bảng Stack của Architecture Spine. i18n `@ngx-translate/core` khớp Consistency Conventions.
- Toàn bộ 16 UX-DR đã có story tương ứng trong epics.md (xem FR Coverage Map + Epic 1/2/3/5).

### Warnings

Không có cảnh báo.

## Epic Quality Review

### Epic Structure Validation

- **User Value Focus:** Cả 5 epic đều đặt tên theo năng lực người dùng (Admin cấu hình / Operator giám sát+ghi nhận / phân tích tổn thất / báo cáo / đồng bộ trung tâm), không có epic kỹ thuật thuần (không có "Setup Database"/"API Development"). Story 1.1 ("Đăng nhập & Khung ứng dụng nền tảng") là trường hợp biên (borderline) — chấp nhận được vì user story thật (đăng nhập, thấy shell đúng vai trò) chứ không phải "setup" trần trụi.
- **Epic Independence:** Epic 2 không cần Epic 3/4/5 để hoạt động; Epic 3 và Epic 4 độc lập với nhau, cả hai chỉ cần output Epic 1+2; Epic 5 dùng output tất cả epic trước — đúng nguyên tắc "epic sau có thể build trên epic trước, không ngược lại" ở **cấp epic**. Tuy nhiên phát hiện 2 vi phạm **ở cấp story** (xem Critical/Major bên dưới) — story trong Epic 1 tham chiếu ngược tới thực thể/API chỉ tồn tại ở Epic 2.

### Story Quality Assessment

- Story sizing hợp lý (3-6 story/epic), mỗi story hoàn thành được trong 1 phiên dev.
- Format Given/When/Then đúng chuẩn, hầu hết testable và cụ thể.
- Database/Entity creation: đúng nguyên tắc "tạo khi cần" — Site/Line/Machine (1.2), ShiftSchedule (1.3), User (1.4), ReasonCode (1.5) ở Epic 1; ProductionCount/DowntimeEvent/QualityReject ở Epic 2; Sync-tracking ở Epic 5. Không có story nào tạo toàn bộ schema upfront.
- CI/CD/Dev-Staging không có story riêng — **không phải thiếu sót**: Architecture Spine mục "Deferred" đã chủ động hoãn quyết định rollout/CI-CD và môi trường Dev/Staging sang giai đoạn sau, nên việc epics.md không có story cho việc này là nhất quán với Architecture, không phải khoảng trống.

### 🔴 Critical Violations

Không có.

### 🟠 Major Issues

**1. Story 1.5 (Epic 1) — AC2 phụ thuộc ngược vào thực thể `DowntimeEvent` chỉ được tạo ở Epic 2, Story 2.5**

> "**Given** Reason Code đã có DowntimeEvent tham chiếu (từ Epic 2) **When** Admin xoá **Then** bị chặn..."

- **Vi phạm:** AC này không thể test được khi Epic 1 hoàn thành độc lập — bảng/entity `DowntimeEvent` chưa tồn tại cho tới khi Epic 2 build xong. Chính text AC cũng tự ghi chú "(từ Epic 2)", tự thừa nhận đây là dữ liệu từ epic tương lai.
- **Impact:** Epic 1 không còn "đứng một mình hoàn toàn" như nguyên tắc yêu cầu — dev thực hiện Story 1.5 sẽ không thể verify AC2 cho tới khi Epic 2 xong.
- **Khuyến nghị:** Tách AC2 thành 2 phần — (a) giữ ở Story 1.5: "Admin có thể deactivate Reason Code (soft-toggle) để ẩn khỏi picker" — testable ngay, không cần DowntimeEvent; (b) chuyển hành vi "chặn xoá cứng nếu đã có DowntimeEvent tham chiếu" thành một AC bổ sung ở **Epic 2, Story 2.5** (nơi DowntimeEvent thực sự được tạo ra) hoặc để ràng buộc FK ở DB tự nhiên xử lý (integrity error) khi Story 2.5 thêm bảng.

**2. Story 1.6 (Epic 1) — AC4 phụ thuộc ngược vào API "dashboard" chỉ được xây ở Epic 2**

> "**Given** tôi gọi thẳng API master-data/**dashboard** với `siteId`/`lineId` ngoài claim JWT... **Then** API vẫn từ chối"

- **Vi phạm:** API dashboard chưa tồn tại khi Story 1.6 hoàn thành (Epic 2 mới xây dashboard). AC này trùng lặp gần như nguyên văn với Epic 2, Story 2.4 AC3 ("Given tôi gọi API xin dữ liệu máy ngoài phạm vi... bị từ chối") — vừa forward-reference vừa dư thừa.
- **Impact:** Story 1.6 không thể test đầy đủ AC4 tại thời điểm Epic 1 hoàn thành.
- **Khuyến nghị:** Xoá phần "/dashboard" khỏi AC4 của Story 1.6 — chỉ giữ lại enforcement cho master-data API (đã testable trong Epic 1). Việc enforcement cho dashboard API đã được Story 2.4 AC3 phủ đầy đủ rồi, không cần lặp lại ở Story 1.6.

### 🟡 Minor Concerns

- Story 1.1 gộp khá nhiều mối quan tâm khác nhau trong 5 AC (login/JWT, error envelope, JWKS rotation, i18n runtime, kiểm tra kiến trúc layer AD-1) — vẫn nằm trong khả năng 1 dev-session nhưng hơi tạp.
- Story 1.1 AC5 ("`OeeNew.Domain` không reference `Infrastructure`/`Api`...") là một kiểm tra tuân thủ kiến trúc/code convention (best verified bằng architecture test, vd. NetArchTest), không phải hành vi người dùng quan sát được qua Given/When/Then thông thường — về bản chất trùng lặp với AD-1 đã có sẵn trong Architecture Spine. Không sai, nhưng khác thể loại so với các AC còn lại trong cùng story.

## Coverage Alignment Note

Sau khi áp dụng khuyến nghị ở 2 Major Issues trên, FR/NFR coverage không đổi (FR-014, FR-015, NFR-5 vẫn được phủ đầy đủ) — đây là vấn đề về **trình tự phụ thuộc giữa story**, không phải thiếu coverage.

## Summary and Recommendations

### Overall Readiness Status

**NEEDS WORK (nhẹ)** — không có Critical Violation, FR/NFR/UX-DR coverage đạt 100%, nhưng có 2 Major Issue (forward-dependency giữa story) cần sửa trong `epics.md` trước khi Sprint Planning khoá trình tự thực thi story.

### Critical Issues Requiring Immediate Action

Không có.

### Major Issues Requiring Action Before Sprint Planning

1. **Story 1.5 AC2** (Epic 1) — tham chiếu `DowntimeEvent` chưa tồn tại cho tới Epic 2. Sửa: tách "deactivate" (giữ ở 1.5) khỏi "chặn xoá cứng nếu có DowntimeEvent tham chiếu" (chuyển sang Epic 2 Story 2.5).
2. **Story 1.6 AC4** (Epic 1) — tham chiếu API "dashboard" chưa tồn tại cho tới Epic 2, trùng lặp với Epic 2 Story 2.4 AC3. Sửa: bỏ "/dashboard" khỏi AC4, chỉ giữ enforcement cho master-data API.

### Recommended Next Steps

1. Sửa 2 Major Issue ở trên trực tiếp trong `epics.md` (có thể quay lại `bmad-create-epics-and-stories` hoặc sửa tay).
2. Chốt `[OPEN QUESTION]` ở PRD mục 7 (timeline triển khai + site thí điểm đầu tiên) — PRD đã tự đánh dấu đây là điều **cần chốt trước Sprint Planning**, chưa chốt sẽ ảnh hưởng thứ tự rollout trong sprint plan.
3. Xác nhận `[ASSUMPTION]` ở FR-019 (Story 3.2) về cách pie chart tổng hợp theo loss category rồi mới drill-down theo mã lý do — đã đề xuất với anh Hoang Thap ở bước tạo epic nhưng chưa có xác nhận final bằng văn bản.
4. Sau khi xử lý mục 1-3, có thể tiến hành **Sprint Planning** (`bmad-sprint-planning`).

### Remediation Applied (2026-07-18)

Cả 2 Major Issue đã được sửa trực tiếp trong `epics.md`:

1. **Story 1.5 AC2** — tách thành: (a) deactivate testable ngay trong Epic 1 (không cần DowntimeEvent); (b) hành vi "chặn xoá cứng nếu đã tham chiếu" chuyển thành AC mới ở **Story 2.5** (nơi DowntimeEvent tồn tại), có ghi chú trỏ ngược về Story 1.5 AC2.
2. **Story 1.6 AC4** — bỏ "/dashboard" khỏi Given/Then, chỉ còn enforcement cho master-data API (testable trong Epic 1); thêm dòng chú thích trỏ sang Story 2.4 AC3 / Story 4.2 AC3 cho phần dashboard/báo cáo, tránh trùng lặp.

Không còn Major/Critical Issue tồn đọng. Overall Readiness Status cập nhật: **READY** (còn 2 mục ở "Recommended Next Steps" #2 và #3 — chốt OPEN QUESTION timeline/pilot site và xác nhận ASSUMPTION pie chart — là quyết định sản phẩm của anh Hoang Thap, không phải lỗi kỹ thuật của epics/stories).

### Final Note

Assessment này phát hiện 2 vấn đề (Major) trên tổng số các hạng mục kiểm tra (Document Discovery, PRD Analysis, Epic Coverage, UX Alignment, Epic Quality Review) — không có vấn đề Critical. Coverage FR/NFR/UX-DR đạt 100%. Các phát hiện này có thể dùng để cải thiện `epics.md`, hoặc anh có thể chọn tiến hành Sprint Planning ngay và xử lý 2 Major Issue trong lúc Dev Story thực thi Story 1.5/1.6 (rủi ro thấp vì phạm vi sửa nhỏ, cục bộ).
