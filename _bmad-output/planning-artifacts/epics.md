---
stepsCompleted: [1, 2, 3]
inputDocuments: [
  "prds/prd-oee-new-2026-07-17/prd.md",
  "prds/prd-oee-new-2026-07-17/addendum.md",
  "architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md",
  "ux-designs/ux-oee-new-2026-07-17/DESIGN.md",
  "ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md"
]
---

# oee-new - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for oee-new, decomposing the requirements from the PRD, UX Design, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-001: Hệ thống phải nhận dữ liệu sản lượng/trạng thái máy từ các máy trong xưởng một cách tự động (không cần nhập tay), theo cấu hình từng máy.
FR-002: Hệ thống phải chuẩn hoá dữ liệu nhận vào bất kể máy sử dụng phương thức kết nối gì, để không phải thay đổi phần tính toán OEE khi thêm loại máy mới. `[ASSUMPTION]` cho phép nhập bù thủ công cho các máy chưa kết nối tự động được, như phương án dự phòng.
FR-003: Hệ thống phải phát hiện và cảnh báo (ở mức hiển thị) khi một máy ngừng gửi dữ liệu quá thời gian quy định (mất kết nối).
FR-004: Operator phải xem được trạng thái và chỉ số OEE của (các) máy mình phụ trách theo thời gian thực, cập nhật trong vài giây khi trạng thái máy thay đổi.
FR-005: Dashboard phải thể hiện trạng thái máy bằng màu sắc rõ ràng (chạy/dừng/cảnh báo), đọc được từ khoảng cách xa trên xưởng.
FR-006: Manager/Viewer phải xem được dashboard tổng hợp nhiều máy/line/site cùng lúc, giới hạn theo site/line được phân quyền.
FR-007: Giao diện phải hỗ trợ song ngữ Việt/Anh, chuyển đổi được theo lựa chọn người dùng.
FR-008: Khi máy dừng, Operator phải chọn lý do dừng máy từ một danh mục mã lý do chuẩn hoá (không phải nhập tự do).
FR-009: Mỗi mã lý do dừng máy phải được phân nhóm vào đúng một trong ba loại tổn thất OEE — Availability loss / Performance loss / Quality loss.
FR-010: Hệ thống phải ghi nhận số lượng sản phẩm lỗi/phế phẩm cơ bản (đủ để tính thành phần Quality của OEE). `[ASSUMPTION]` module quản lý chất lượng chi tiết không thuộc phạm vi MVP.
FR-011: Admin phải quản lý (thêm/sửa/xoá) được danh mục Site, Line, Machine theo cấu trúc phân cấp Site > Line > Machine.
FR-012: Admin phải quản lý được ca làm việc (shift schedule) áp dụng cho từng site/line.
FR-013: Admin phải quản lý được người dùng và gán vai trò (Admin/Manager/Operator/Viewer) kèm phạm vi site/line tương ứng.
FR-014: Admin phải quản lý được danh mục mã lý do dừng máy và nhóm tổn thất tương ứng (liên kết FR-009).
FR-015: Hệ thống phải từ chối thao tác ngoài phạm vi site/line được phân quyền của người dùng, ở cả giao diện lẫn tầng xử lý phía sau.
FR-016: Manager/Viewer phải xem được báo cáo OEE tổng hợp theo ca, theo ngày, và theo tuần.
FR-017: Báo cáo phải cho phép lọc theo site/line/máy trong phạm vi được phân quyền.
FR-018: Báo cáo phải hiển thị được nguyên nhân dừng máy chiếm nhiều thời gian nhất trong kỳ báo cáo.
FR-019: Dashboard phải cung cấp biểu đồ dạng pie chart thể hiện tỷ lệ phân bổ tổn thất/thời gian. `[ASSUMPTION]` theo 3 loại tổn thất OEE và/hoặc theo mã lý do dừng máy — cần xác nhận.
FR-020: Người dùng phải chọn xem pie chart theo Equipment (máy cụ thể) hoặc theo Production Area (khu vực/line), trong phạm vi site/line được phân quyền.
FR-021: Người dùng phải xem được chi tiết theo ngày (drill-down chọn một ngày cụ thể để xem lại phân bổ pie chart của ngày đó).

### NonFunctional Requirements

NFR-1 (Real-time): Thay đổi trạng thái máy phải phản ánh lên dashboard trong vòng vài giây, không yêu cầu người dùng tải lại trang.
NFR-2 (Multi-site): Hệ thống phải hỗ trợ nhiều nhà máy hoạt động đồng thời ngay từ ngày ra mắt, với khả năng xem tổng hợp toàn hệ thống cho Admin.
NFR-3 (Triển khai on-premise): Mỗi site có hạ tầng (server/DB) on-premise riêng, hoạt động độc lập không phụ thuộc kết nối liên site. Dữ liệu đồng bộ định kỳ về một điểm tổng hợp.
NFR-4 (Chịu lỗi mạng xưởng): Ghi nhận lý do dừng máy tại xưởng không được phụ thuộc hoàn toàn vào kết nối ra ngoài site.
NFR-5 (Phân quyền): Mọi API/màn hình phải thực thi kiểm soát truy cập theo vai trò và phạm vi site/line — không chỉ ẩn/hiện ở giao diện.
NFR-6 (Song ngữ): Toàn bộ giao diện người dùng phải hỗ trợ tiếng Việt và tiếng Anh.

### Additional Requirements

- Stack đã chốt: PostgreSQL 18.4, .NET 10.0 (ASP.NET Core Web API + EF Core 10.0), Angular 21 + PrimeNG 21, SignalR (đi kèm ASP.NET Core).
- Kiến trúc: Layered Modular Monolith replicated per site (`OeeNew.Domain` → `OeeNew.Application` → `OeeNew.Infrastructure`/`OeeNew.Api`), cùng codebase chạy `AppMode: Site | Central`. Domain không reference Infrastructure/Api (AD-1).
- Source tree/starter seed đã chốt cho Epic 1 Story 1: `src/OeeNew.Domain|Application|Infrastructure|Api`, `web/oee-shell` (Angular, module `dashboard/downtime/master-data/reports`).
- Ingestion Adapter Pattern (AD-3): interface `IProductionDataSource` (machine_id, timestamp, counter [cumulative], status enum `Running|Stopped|Idle|Fault`) trong Domain/Application; giao thức máy thật (OPC-UA/MQTT/Modbus/thủ công) là chi tiết Infrastructure, triển khai sau MVP. Cần nguồn ingestion tạm (form nhập tay hoặc script giả lập) để dev/test dashboard không bị chặn.
- Phân cấp dữ liệu Site > Line > Machine model trong DB từ ngày một (theo ISO 22400).
- Site tự trị, trung tâm chỉ tổng hợp (AD-2): mỗi site có Postgres riêng, vận hành đầy đủ offline; Sync chỉ đẩy lên trung tâm bản ghi nghiệp vụ đã chốt (DowntimeEvent đã đóng, ProductionCount theo khung giờ, QualityReject) — không phải luồng tín hiệu thô.
- Master data thuộc sở hữu từng site (AD-4): Site/Line/Machine/ShiftSchedule/ReasonCode/role-scoping ghi tại site; trung tâm chỉ đọc qua Sync một chiều, không có endpoint proxy ghi ngược xuống site (kể cả JWT Admin toàn cục).
- Reason code cục bộ, taxonomy tổn thất toàn cục (AD-5): cột `LossCategory` trên `ReasonCode` NOT NULL ở schema DB, enum `AvailabilityLoss|PerformanceLoss|QualityLoss`.
- Định danh GUID cho mọi entity đồng bộ (AD-6): `uuidv7()` sinh tại PostgreSQL 18 (không dùng `Guid.CreateVersion7()` .NET), sinh tại site lúc tạo record.
- Identity tập trung, xác thực offline-first tại site (AD-7): Identity Provider trung tâm tạo/lưu credential (username/password hash, token issuance) cho mọi user; JWT chứa claim `role` (Admin toàn cục) + `siteId`/`lineIds`; site + trung tâm cache tối thiểu 2 signing key từ JWKS; authorization policy-based enforce tại site.
- Real-time qua SignalR theo site (AD-8): mỗi site instance có SignalR hub riêng; event `MachineStatusChanged`, `DowntimeReasonRecorded`.
- Consistency conventions: API error envelope chuẩn `{ code, message, details? }`; ngày giờ ISO 8601 UTC lưu trữ, giờ địa phương hiển thị; i18n resource key `feature.section.label` qua `@ngx-translate/core` (không dùng Angular built-in i18n).
- Deployment: mỗi site 1 server on-premise (`AppMode=Site`) + Postgres 18 local; trung tâm 1 instance riêng (`AppMode=Central`) + Postgres riêng; Identity Provider đồng hành cùng trung tâm.
- Backup/DR: mỗi site tự backup Postgres cục bộ (bắt buộc quyết định trước go-live — tần suất/retention để lại Sprint Planning/Ops).
- Observability tối thiểu: trung tâm phải thấy "site nào mất kết nối/mất đồng bộ bao lâu" dựa trên last-sync-timestamp (hệ quả bắt buộc của AD-2).
- Core-Entity ERD: Site→Line→Machine, Site→ShiftSchedule, Site→User→Role, Site→ReasonCode→LossCategory, Machine→ProductionCount/DowntimeEvent(→ReasonCode)/QualityReject.

### UX Design Requirements

UX-DR1: Kế thừa layout Sakai-NG (topbar + sidebar thu gọn được + footer + vùng nội dung chính), Angular 21 + PrimeNG 21 — không thiết kế nhận diện riêng ngoài phần brand-layer delta.
UX-DR2: Sidebar hiển thị theo vai trò — Dashboard (Operator/Manager/Viewer/Admin), Downtime lịch sử (Operator chỉ xem của mình; Manager/Viewer/Admin xem đầy đủ), Reports (Manager/Viewer/Admin, không Operator), Master Data (chỉ Admin).
UX-DR3: Topbar Site/Line selector — progressive disclosure, chỉ hiện khi user có quyền trên >1 site/line; ẩn hoàn toàn khi chỉ có 1 site.
UX-DR4: Chuyển ngôn ngữ Việt/Anh tại topbar, runtime, không reload trang, dùng `@ngx-translate/core`.
UX-DR5: Central instance (Admin xem xuyên site) — Master Data hiển thị read-only, kèm link "Mở tại site X" để Admin thao tác ghi trực tiếp tại site đó (hệ quả AD-4).
UX-DR6: Machine Status Card — nền đổi màu theo trạng thái (`status-running/stopped/idle/no-signal`), luôn kèm icon + nhãn chữ (không dùng màu làm tín hiệu duy nhất), `minTouchTarget: 96px`, padding 24px, hiệu ứng pulse nhẹ khi cập nhật real-time (không giật).
UX-DR7: Reason Code Picker — mở full-screen khi chạm Machine Status Card đang `Stopped`, lưới nút lớn nhóm theo `LossCategory` (Availability/Performance/Quality), chạm 1 lần để chọn và tự đóng, không có bước xác nhận phụ, `minTouchTarget: 64px`.
UX-DR8: Loss Pie Chart (FR-019..021) — PrimeNG Chart (Chart.js), dropdown lọc theo Equipment hoặc Production Area, date-picker chọn ngày cụ thể để drill-down, palette 3 màu cố định (`loss-availability` đỏ, `loss-performance` vàng, `loss-quality` tím) không đổi theo theme sáng/tối, drill-down bằng chạm/click trực tiếp vào lát pie chart.
UX-DR9: No-signal state — card màu xám (`status-no-signal`) + icon mất kết nối + nhãn "Mất tín hiệu Xp", tách biệt rõ khỏi `status-stopped` (đỏ) để không nhầm mất tín hiệu thành máy dừng thật (FR-003).
UX-DR10: Empty state — chưa có máy nào được gán cho user, hiển thị hướng dẫn liên hệ Admin thay vì để trắng trơn.
UX-DR11: Sync/Last-updated Badge (Manager/Admin xem báo cáo xuyên site) — hiển thị "Đồng bộ lần cuối: X phút trước" theo site; chuyển màu cảnh báo (không phải lỗi) nếu quá lâu chưa đồng bộ.
UX-DR12: Loading state — skeleton card khi dashboard đang chờ dữ liệu real-time đầu tiên từ SignalR.
UX-DR13: Accessibility floor — độ tương phản màu trạng thái đạt tối thiểu AA trên cả theme sáng/tối của Sakai; màu không bao giờ là tín hiệu duy nhất (đi kèm icon + nhãn chữ).
UX-DR14: Typography `shopfloor-display` (56px, đậm) cho số OEE/tên máy chính, `shopfloor-label` (20px) cho nhãn phụ trên dashboard xưởng — đọc được từ khoảng cách vài mét.
UX-DR15: Touch-first trên màn hình xưởng — không thao tác nào yêu cầu double-tap, drag, hay gõ chữ để hoàn thành ghi nhận downtime.
UX-DR16: Cập nhật đẩy (push) không kéo (pull) — không có nút "Refresh" thủ công trên dashboard; dữ liệu tự cập nhật qua SignalR.

### FR Coverage Map

FR-001: Epic 2 - Ingestion tự động từ máy
FR-002: Epic 2 - Chuẩn hoá dữ liệu ingestion, cho phép nhập bù thủ công
FR-003: Epic 2 - Phát hiện/cảnh báo mất tín hiệu máy
FR-004: Epic 2 - Operator xem trạng thái/OEE real-time máy mình phụ trách
FR-005: Epic 2 - Màu trạng thái máy rõ ràng, đọc từ xa
FR-006: Epic 2 - Manager/Viewer xem dashboard tổng hợp theo site/line phân quyền
FR-007: Epic 1 - Song ngữ Việt/Anh
FR-008: Epic 2 - Operator chọn lý do dừng máy từ danh mục chuẩn hoá
FR-009: Epic 2 - Mã lý do dừng máy phân nhóm theo loss category
FR-010: Epic 2 - Ghi nhận sản phẩm lỗi/phế phẩm cơ bản
FR-011: Epic 1 - Admin quản lý Site/Line/Machine
FR-012: Epic 1 - Admin quản lý ca làm việc
FR-013: Epic 1 - Admin quản lý người dùng + vai trò + phạm vi site/line
FR-014: Epic 1 - Admin quản lý danh mục mã lý do dừng máy + nhóm tổn thất
FR-015: Epic 1 - Từ chối thao tác ngoài phạm vi phân quyền (UI + backend)
FR-016: Epic 4 - Báo cáo OEE tổng hợp theo ca/ngày/tuần
FR-017: Epic 4 - Lọc báo cáo theo site/line/máy
FR-018: Epic 4 - Hiển thị nguyên nhân dừng máy chiếm nhiều thời gian nhất
FR-019: Epic 3 - Pie chart tỷ lệ tổn thất/thời gian
FR-020: Epic 3 - Lọc pie chart theo Equipment/Production Area
FR-021: Epic 3 - Drill-down pie chart theo ngày cụ thể

NFR-1: Epic 2 - Real-time cập nhật dashboard
NFR-2: Epic 1 (nền tảng model đa site) + Epic 5 (tổng hợp đầy đủ xuyên site)
NFR-3: Epic 2 (site tự vận hành độc lập) + Epic 5 (cơ chế Sync đầy đủ)
NFR-4: Epic 2 - Chịu lỗi mạng xưởng khi ghi downtime
NFR-5: Epic 1 - Phân quyền enforce ở API
NFR-6: Epic 1 - Hạ tầng song ngữ toàn giao diện

## Epic List

### Epic 1: Cấu hình Master Data & Nền tảng ứng dụng

Admin thiết lập được toàn bộ danh mục Site/Line/Machine, ca làm việc, người dùng+vai trò, mã lý do dừng máy — đồng thời khung ứng dụng (shell Sakai-NG, sidebar theo vai trò, song ngữ Việt/Anh, xác thực JWT) sẵn sàng cho mọi vai trò đăng nhập và điều hướng đúng theo quyền.

**FRs covered:** FR-007, FR-011, FR-012, FR-013, FR-014, FR-015
**NFRs covered:** NFR-5, NFR-2 (nền tảng), NFR-6
**Kỹ thuật:** project scaffold (source tree theo Architecture Spine), Identity Provider JWT/JWKS (AD-7), GUID `uuidv7()` (AD-6), Site>Line>Machine hierarchy, API error envelope chuẩn, i18n `@ngx-translate/core`
**UX:** UX-DR1, UX-DR2, UX-DR3, UX-DR4

### Epic 2: Giám sát thời gian thực & Ghi nhận downtime

Operator thấy trạng thái máy mình phụ trách cập nhật theo thời gian thực (kể cả khi mất tín hiệu), và ghi nhận lý do dừng máy chỉ bằng vài lần chạm — đầy đủ vòng lặp UJ-1.

**FRs covered:** FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-008, FR-009, FR-010
**NFRs covered:** NFR-1, NFR-3 (site tự vận hành độc lập), NFR-4
**UX:** UX-DR6, UX-DR7, UX-DR9, UX-DR10, UX-DR12, UX-DR13, UX-DR14, UX-DR15, UX-DR16

### Epic 3: Phân tích tổn thất OEE (Pie Chart)

Người dùng xem được tỷ lệ tổn thất Availability/Performance/Quality theo Equipment hoặc Production Area, và drill-down xem chi tiết theo ngày cụ thể.

**FRs covered:** FR-019, FR-020, FR-021
**UX:** UX-DR8

### Epic 4: Báo cáo OEE theo ca/ngày/tuần

Manager/Viewer xem được báo cáo OEE tổng hợp theo ca/ngày/tuần, lọc theo site/line/máy, và thấy ngay nguyên nhân dừng máy chiếm nhiều thời gian nhất — phục vụ họp giao ca.

**FRs covered:** FR-016, FR-017, FR-018

### Epic 5: Đồng bộ đa site & Tổng hợp trung tâm

Admin/Manager xem được dashboard/báo cáo tổng hợp xuyên site tại instance trung tâm, biết site nào đang mất đồng bộ bao lâu, và Master Data ở trung tâm hiển thị read-only kèm link thao tác trực tiếp tại site.

**NFRs covered:** NFR-2 (đầy đủ), NFR-3 (đầy đủ — cơ chế Sync)
**Kỹ thuật:** Sync module, `AppMode=Central`, observability last-sync-timestamp
**UX:** UX-DR5, UX-DR11

## Epic 1: Cấu hình Master Data & Nền tảng ứng dụng

Admin thiết lập được toàn bộ danh mục Site/Line/Machine, ca làm việc, người dùng+vai trò, mã lý do dừng máy — đồng thời khung ứng dụng (shell Sakai-NG, sidebar theo vai trò, song ngữ Việt/Anh, xác thực JWT) sẵn sàng cho mọi vai trò đăng nhập và điều hướng đúng theo quyền.

### Story 1.1: Đăng nhập & Khung ứng dụng nền tảng

As a user (Admin/Manager/Operator/Viewer),
I want to log in with credentials issued by the central Identity Provider and see the app shell reflecting my role,
So that I have a secure, correctly-scoped starting point before using any feature.

**Acceptance Criteria:**

**Given** credentials hợp lệ do Identity Provider trung tâm cấp
**When** tôi đăng nhập
**Then** tôi nhận JWT chứa claim `role` + `siteId`/`lineIds` (AD-7)
**And** shell Sakai-NG (topbar+sidebar+footer+content) tải lên với sidebar chỉ hiện mục được phép theo vai trò (UX-DR2)

**Given** tôi đã đăng nhập
**When** app gọi API
**Then** mọi request kèm JWT Bearer
**And** mọi lỗi trả về theo envelope chuẩn `{ code, message, details? }`

**Given** signing key vừa rotate
**When** tôi dùng token ký bằng key trước đó (còn hạn)
**Then** hệ thống vẫn chấp nhận vì cache tối thiểu 2 key JWKS (AD-7)

**Given** tôi bấm chuyển ngôn ngữ ở topbar
**When** chuyển Việt↔Anh
**Then** toàn bộ text cập nhật ngay không reload trang (FR-007, UX-DR4)

**Given** source tree đã chốt ở Architecture Spine
**When** scaffold backend/frontend
**Then** `OeeNew.Domain` không reference `Infrastructure`/`Api`, `Application` chỉ phụ thuộc `Domain` + interface (AD-1)

### Story 1.2: Quản lý danh mục Site/Line/Machine

As an Admin,
I want to create/edit/delete Sites, Lines, and Machines in a Site > Line > Machine hierarchy,
So that the rest of the system has a structured asset reference.

**Acceptance Criteria:**

**Given** tôi là Admin
**When** tạo Site mới
**Then** bản ghi có khoá chính `uuidv7()` (AD-6) và xuất hiện trong danh sách

**Given** Site đã tồn tại
**When** tạo Line
**Then** Line gắn với Site cha, không tạo được nếu thiếu Site cha hợp lệ

**Given** Line đã tồn tại
**When** tạo Machine
**Then** Machine gắn với Line cha

**Given** tôi là Manager/Operator/Viewer
**When** gọi API tạo/sửa/xoá Site/Line/Machine
**Then** bị từ chối (FR-015, NFR-5) — enforce ở tầng API

**Given** Line còn Machine con
**When** Admin xoá Line
**Then** bị chặn kèm danh sách Machine phụ thuộc
**And** `[ASSUMPTION]` không cascade-delete ở MVP để tránh mất dữ liệu

### Story 1.3: Quản lý ca làm việc (Shift Schedule)

As an Admin,
I want to define shift schedules per Site/Line,
So that reports and dashboards group data by đúng ca.

**Acceptance Criteria:**

**Given** tôi là Admin
**When** tạo ca (tên + giờ bắt đầu/kết thúc) cho một Site (tuỳ chọn scope theo Line)
**Then** ca được lưu và sẵn sàng cho báo cáo (Epic 4)

**Given** hai ca cùng Site/Line có khung giờ chồng lấn
**When** lưu ca thứ hai
**Then** bị từ chối với lỗi rõ ràng
**And** `[ASSUMPTION]` tránh đếm trùng thời gian sản xuất

**Given** tôi là Manager/Operator/Viewer
**When** gọi API tạo/sửa/xoá ca
**Then** bị từ chối (FR-015, NFR-5)

### Story 1.4: Quản lý người dùng, vai trò & phạm vi site/line

As an Admin,
I want to create users, assign role (Admin/Manager/Operator/Viewer), and scope Manager/Operator/Viewer to Site(s)/Line(s),
So that each user only sees/acts within their scope.

**Acceptance Criteria:**

**Given** tôi là Admin
**When** tạo user role=Operator gán vào Line X
**Then** role-scoping lưu tại site (AD-4) và JWT tương lai của user chứa `siteId`/`lineIds` khớp assignment

**Given** user mới lần đầu được tạo tại site
**When** site online và liên lạc được trung tâm ít nhất 1 lần
**Then** credential được tạo tại Identity Provider trung tâm, user đăng nhập được kể cả khi site sau đó offline (AD-7)

**Given** user role=Admin
**When** JWT phát hành
**Then** claim `role: Admin` là toàn cục, không giới hạn theo site (AD-7)

**Given** tôi là Manager/Operator/Viewer
**When** gọi API tạo/sửa user hoặc đổi role
**Then** bị từ chối (FR-015, NFR-5)

### Story 1.5: Quản lý mã lý do dừng máy & nhóm tổn thất

As an Admin,
I want to create/edit/delete downtime reason codes and assign each to exactly one loss category,
So that downtime rolls up correctly into the OEE formula.

**Acceptance Criteria:**

**Given** tôi là Admin
**When** tạo Reason Code mới
**Then** bắt buộc chọn đúng 1 `LossCategory` (AvailabilityLoss|PerformanceLoss|QualityLoss), NOT NULL ở schema DB (AD-5) — không thể để trống kể cả gọi API trực tiếp

**Given** Reason Code không còn cần dùng nữa
**When** Admin deactivate (thay vì xoá cứng)
**Then** Reason Code ẩn khỏi Reason Code Picker của Operator (Epic 2, Story 2.5) nhưng vẫn giữ nguyên trong dữ liệu lịch sử `[ASSUMPTION]`

**Given** tôi là Manager/Operator/Viewer
**When** gọi API tạo/sửa/xoá Reason Code
**Then** bị từ chối (FR-015, NFR-5)

### Story 1.6: Site/Line Selector & Thực thi phạm vi truy cập toàn hệ thống

As a Manager/Admin with access to more than one Site/Line,
I want a topbar selector that only appears when relevant,
So that I can switch context without clutter for single-site users.

**Acceptance Criteria:**

**Given** tôi chỉ có quyền trên 1 Site
**When** bất kỳ màn hình nào tải
**Then** selector ẩn hoàn toàn (UX-DR3)

**Given** tôi có quyền trên >1 Site
**When** mở selector
**Then** chỉ thấy Site/Line trong claim `siteId`/`lineIds` của JWT — không bao giờ thấy site ngoài phạm vi

**Given** tôi đổi Site/Line trong selector
**When** selection thay đổi
**Then** mọi danh sách master-data (1.2-1.5) và màn hình dashboard/báo cáo sau này lọc lại theo scope mới

**Given** tôi gọi thẳng API master-data với `siteId`/`lineId` ngoài claim JWT (bỏ qua UI selector)
**When** request được gửi
**Then** API vẫn từ chối (FR-015, NFR-5) — xác nhận enforcement ở server, không chỉ ẩn/hiện UI
**And** cùng nguyên tắc này áp dụng cho API dashboard/báo cáo khi các API đó được xây ở Epic 2/4 (xem Story 2.4 AC3, Story 4.2 AC3) — không lặp lại kiểm thử ở đây

## Epic 2: Giám sát thời gian thực & Ghi nhận downtime

Operator thấy trạng thái máy mình phụ trách cập nhật theo thời gian thực (kể cả khi mất tín hiệu), và ghi nhận lý do dừng máy chỉ bằng vài lần chạm — đầy đủ vòng lặp UJ-1.

### Story 2.1: Nhận & chuẩn hoá dữ liệu sản xuất từ máy (Ingestion)

As an Operator/system integrator,
I want the system to accept machine production/status data through a single standardized endpoint,
So that OEE calculation logic never changes when a new machine type or protocol is added.

**Acceptance Criteria:**

**Given** máy gửi dữ liệu tới ingestion endpoint với các field machine_id/timestamp/counter/status
**When** hệ thống nhận
**Then** dữ liệu được xử lý qua interface `IProductionDataSource`, Domain/Application không biết chi tiết giao thức (AD-3)

**Given** giá trị counter
**When** xử lý
**Then** được hiểu là giá trị luỹ kế (cumulative), không phải delta (AD-3)

**Given** giá trị status
**When** xử lý
**Then** phải là một trong enum `Running|Stopped|Idle|Fault`; giá trị khác bị từ chối theo error envelope chuẩn

**Given** một máy chưa kết nối tự động được
**When** Operator/Admin nhập bù sản lượng/trạng thái thủ công qua form
**Then** dữ liệu đi qua đúng luồng domain giống hệt ingestion tự động (FR-002)

**Given** trung tâm (Central) không kết nối được
**When** Operator ghi nhận downtime/dashboard cập nhật tại site
**Then** mọi thao tác vẫn hoạt động đầy đủ (NFR-3, NFR-4) — site không phụ thuộc trung tâm cho vận hành hàng ngày

### Story 2.2: Dashboard hiển thị trạng thái máy real-time

As an Operator,
I want to see my assigned machine(s) status update within seconds without refreshing,
So that I always know the current operating state at a glance from a distance.

**Acceptance Criteria:**

**Given** trạng thái máy thay đổi
**When** SignalR hub phát broadcast `MachineStatusChanged` (AD-8)
**Then** Machine Status Card cập nhật trong vài giây không cần tải lại trang (FR-004, NFR-1, UX-DR16)

**Given** trạng thái máy
**When** hiển thị
**Then** nền đổi màu theo status-running/stopped/idle **and** luôn kèm icon + nhãn chữ (không dùng màu làm tín hiệu duy nhất) (UX-DR13)

**Given** trạng thái máy vừa đổi
**When** card cập nhật
**Then** hiệu ứng pulse nhẹ chạy một lần, không giật (UX-DR6)

**Given** dashboard đang chờ dữ liệu real-time đầu tiên
**When** đang tải
**Then** hiển thị skeleton card (UX-DR12)

**Given** card ở màn hình xưởng
**When** hiển thị
**Then** `minTouchTarget` ≥96px, số OEE dùng typography `shopfloor-display` 56px đọc được từ vài mét (UX-DR14)

### Story 2.3: Phát hiện & hiển thị mất tín hiệu máy

As an Operator/Admin,
I want a machine that stops sending data for too long to show a distinct "no signal" state,
So that I don't mistake a connectivity problem for a real stoppage.

**Acceptance Criteria:**

**Given** một máy không gửi dữ liệu quá thời gian cấu hình
**When** vượt ngưỡng
**Then** card chuyển `status-no-signal` (xám) + icon mất kết nối + nhãn "Mất tín hiệu Xp" (FR-003, UX-DR9)

**Given** máy đang ở trạng thái no-signal
**When** so với `status-stopped` (đỏ)
**Then** tách biệt rõ về màu sắc/ngữ nghĩa — no-signal không bao giờ được tính là DowntimeEvent thật

**Given** máy gửi dữ liệu trở lại
**When** dữ liệu mới đến
**Then** card quay về đúng trạng thái thật (Running/Stopped/Idle) theo báo cáo mới nhất

### Story 2.4: Dashboard tổng hợp nhiều máy/line theo phân quyền

As a Manager/Viewer,
I want to see a dashboard of multiple machines/lines at once, limited to my assigned site/line scope,
So that I can monitor my area without seeing unrelated sites.

**Acceptance Criteria:**

**Given** tôi là Manager/Viewer có quyền trên Line A và B
**When** mở dashboard
**Then** chỉ thấy máy thuộc Line A/B (FR-006, dùng scope đã enforce ở Story 1.6)

**Given** tôi chưa được gán máy nào
**When** dashboard tải
**Then** hiển thị empty state hướng dẫn liên hệ Admin (UX-DR10), không để trắng trơn

**Given** tôi gọi API xin dữ liệu máy ngoài phạm vi
**When** request được gửi
**Then** bị từ chối (FR-015, NFR-5)

### Story 2.5: Ghi nhận lý do dừng máy (Reason Code Picker)

As an Operator,
I want to select a downtime reason from a large grouped button grid when my machine stops,
So that I can record it in a few seconds without typing or leaving my position.

**Acceptance Criteria:**

**Given** card máy của tôi đang `Stopped`
**When** tôi chạm vào card
**Then** Reason Code Picker mở full-screen, hiện các Reason Code đang active nhóm theo `LossCategory` (Availability/Performance/Quality) (FR-008, FR-009, UX-DR7)

**Given** picker đang mở
**When** tôi chạm 1 lần vào một nút lý do
**Then** DowntimeEvent được ghi nhận với ReasonCode đó, picker tự đóng, không có bước xác nhận phụ (UX-DR7)

**Given** một DowntimeEvent đang mở (máy còn dừng) và máy chạy lại
**When** trạng thái Running được nhận
**Then** DowntimeEvent tự đóng kèm timestamp kết thúc (khớp bản ghi nghiệp vụ đã chốt theo AD-2)

**Given** các nút lý do được hiển thị
**When** render
**Then** mỗi nút có `minTouchTarget` ≥64px (UX-DR7)

**Given** một Reason Code đã có ít nhất 1 DowntimeEvent tham chiếu
**When** Admin cố xoá cứng Reason Code đó (Epic 1, Story 1.5)
**Then** bị chặn để bảo toàn báo cáo lịch sử — Admin chỉ có thể deactivate, không xoá được (bổ sung cho Story 1.5 AC2, chỉ testable từ đây trở đi vì DowntimeEvent mới tồn tại)

### Story 2.6: Ghi nhận số lượng sản phẩm lỗi/phế phẩm

As an Operator,
I want to record basic reject/scrap quantity for my machine,
So that the Quality component of OEE can be calculated.

**Acceptance Criteria:**

**Given** máy của tôi đang chạy
**When** tôi nhập số lượng phế phẩm cơ bản (không chi tiết root-cause — ngoài phạm vi MVP theo PRD)
**Then** một bản ghi QualityReject được lưu, gắn với máy và khung thời gian/ca hiện tại (FR-010)

**Given** tôi là Manager/Operator/Viewer ngoài phạm vi được gán
**When** tôi cố ghi QualityReject cho máy không thuộc phạm vi mình
**Then** bị từ chối (FR-015, NFR-5)

## Epic 3: Phân tích tổn thất OEE (Pie Chart)

Người dùng xem được tỷ lệ tổn thất Availability/Performance/Quality theo Equipment hoặc Production Area, và drill-down xem chi tiết theo ngày cụ thể.

### Story 3.1: Xem pie chart tổn thất theo Equipment/Production Area

As a user (Operator/Manager/Viewer),
I want to view a pie chart of Availability/Performance/Quality loss, filterable by Equipment or Production Area,
So that I can see where time is being lost.

**Acceptance Criteria:**

**Given** tôi mở Dashboard
**When** tôi chọn xem theo một Equipment cụ thể
**Then** pie chart hiển thị tỷ lệ 3 loại tổn thất (Availability/Performance/Quality) tính từ DowntimeEvent + QualityReject của máy đó (FR-019, FR-020)

**Given** tôi chọn xem theo Production Area (Line)
**When** chọn
**Then** pie chart tổng hợp tất cả máy thuộc line đó (FR-020)

**Given** pie chart hiển thị
**When** render
**Then** dùng palette cố định `loss-availability` (đỏ)/`loss-performance` (vàng)/`loss-quality` (tím), không đổi theo theme sáng/tối (UX-DR8)

**Given** tôi chỉ có quyền trên site/line nhất định
**When** chọn Equipment/Area để xem
**Then** dropdown chỉ liệt kê Equipment/Area trong phạm vi được phân quyền (FR-020, NFR-5)

### Story 3.2: Drill-down pie chart theo ngày & theo mã lý do

As a user,
I want to pick a specific date and tap into a pie slice,
So that I can inspect that day's loss breakdown by reason code without it blending into a broader range.

**Acceptance Criteria:**

**Given** pie chart đang hiển thị tổng hợp nhiều ngày
**When** tôi chọn một ngày cụ thể qua date-picker
**Then** pie chart chỉ hiển thị dữ liệu tổn thất của riêng ngày đó (FR-021)

**Given** tôi chạm/click vào một lát pie chart (vd Availability)
**When** thao tác
**Then** hiển thị breakdown theo từng mã lý do dừng máy góp phần vào lát đó, không cần menu phụ (UX-DR8)
**And** `[ASSUMPTION]` giải quyết câu hỏi mở FR-019 trong PRD — mức tổng là 3 loss category, drill-down theo lát mới xuống mã lý do

**Given** không có dữ liệu tổn thất cho ngày được chọn
**When** pie chart render
**Then** hiển thị empty state rõ ràng thay vì biểu đồ trống khó hiểu

## Epic 4: Báo cáo OEE theo ca/ngày/tuần

Manager/Viewer xem được báo cáo OEE tổng hợp theo ca/ngày/tuần, lọc theo site/line/máy, và thấy ngay nguyên nhân dừng máy chiếm nhiều thời gian nhất — phục vụ họp giao ca.

### Story 4.1: Xem báo cáo OEE tổng hợp theo ca/ngày/tuần

As a Manager/Viewer,
I want to view aggregated OEE reports by shift/day/week,
So that I can review performance without manual Excel compilation.

**Acceptance Criteria:**

**Given** tôi là Manager/Viewer
**When** tôi mở Reports
**Then** tôi thấy báo cáo OEE (Availability×Performance×Quality) tổng hợp theo Ca, theo Ngày, và theo Tuần (FR-016)

**Given** tôi chọn kỳ báo cáo
**When** chọn Ca cụ thể
**Then** số liệu tính đúng theo Shift Schedule đã cấu hình ở Story 1.3

**Given** tôi là Operator
**When** tôi cố mở Reports
**Then** bị từ chối truy cập màn hình theo IA đã chốt (chỉ Manager/Viewer/Admin) (FR-015)

### Story 4.2: Lọc báo cáo theo site/line/máy

As a Manager/Viewer,
I want to filter the report by site/line/machine within my permission scope,
So that I can narrow down to what I actually manage.

**Acceptance Criteria:**

**Given** tôi có quyền trên nhiều site/line
**When** tôi mở bộ lọc báo cáo
**Then** chỉ thấy các lựa chọn trong phạm vi được phân quyền (FR-017, NFR-5)

**Given** tôi áp dụng lọc theo Machine cụ thể
**When** áp dụng
**Then** báo cáo chỉ tính trên máy đó

**Given** tôi gọi API báo cáo trực tiếp với site/line ngoài phạm vi
**When** gửi request
**Then** bị từ chối (FR-015, NFR-5)

### Story 4.3: Xem nguyên nhân dừng máy chiếm nhiều thời gian nhất

As a Manager,
I want the report to surface the top downtime reason for the period,
So that I can bring it directly into the shift handover meeting without compiling Excel manually.

**Acceptance Criteria:**

**Given** báo cáo đã được tạo cho một kỳ (ca/ngày/tuần)
**When** tôi xem report
**Then** hệ thống hiển thị mã lý do dừng máy chiếm nhiều thời gian nhất trong kỳ đó, kèm tổng thời gian (FR-018)

**Given** nhiều mã lý do có tổng thời gian bằng nhau
**When** xếp hạng
**Then** hiển thị theo thứ tự ổn định (vd. theo tên) để không gây nhầm lẫn giữa các lần xem
**And** `[ASSUMPTION]`

**Given** không có DowntimeEvent nào trong kỳ
**When** report render
**Then** hiển thị "Không có dữ liệu dừng máy" thay vì lỗi hoặc trống khó hiểu

## Epic 5: Đồng bộ đa site & Tổng hợp trung tâm

Admin/Manager xem được dashboard/báo cáo tổng hợp xuyên site tại instance trung tâm, biết site nào đang mất đồng bộ bao lâu, và Master Data ở trung tâm hiển thị read-only kèm link thao tác trực tiếp tại site.

### Story 5.1: Đồng bộ dữ liệu site lên trung tâm

As an Admin at Central,
I want each site to periodically sync completed business records to the central instance,
So that I can view cross-site aggregated data without depending on constant site connectivity.

**Acceptance Criteria:**

**Given** site instance hoạt động ở `AppMode=Site`
**When** đến chu kỳ đồng bộ
**Then** Sync module đẩy lên trung tâm các bản ghi nghiệp vụ đã chốt: DowntimeEvent đã đóng, ProductionCount theo khung giờ, QualityReject — không phải luồng tín hiệu thô (AD-2)

**Given** site mất kết nối tới trung tâm
**When** Sync không thực hiện được
**Then** site vẫn tiếp tục vận hành đầy đủ (ingestion/dashboard/downtime), dữ liệu chờ đồng bộ ở lần kế tiếp có kết nối (NFR-3)

**Given** entity được đồng bộ
**When** ghi vào DB trung tâm
**Then** dùng cùng khoá `uuidv7()` sinh tại site (AD-6), không đụng độ khoá chính giữa các site

**Given** Site/Line/Machine/ReasonCode master data thay đổi tại site
**When** đồng bộ
**Then** trung tâm cập nhật bản đọc (read-only) tương ứng, không có endpoint nào ở trung tâm ghi ngược lại site (AD-4)

### Story 5.2: Dashboard/Báo cáo tổng hợp xuyên site tại trung tâm

As an Admin/Manager at the Central instance,
I want to view aggregated dashboards/reports spanning all my authorized sites,
So that I can see the whole operation without visiting each site separately.

**Acceptance Criteria:**

**Given** tôi đăng nhập vào instance Central (`AppMode=Central`) với quyền trên nhiều site
**When** tôi mở Dashboard/Reports
**Then** dữ liệu tổng hợp từ tất cả site tôi được phân quyền hiển thị cùng lúc (NFR-2)

**Given** dữ liệu tại trung tâm chỉ cập nhật theo chu kỳ Sync
**When** tôi xem
**Then** không có kỳ vọng cập nhật real-time cho dữ liệu xuyên site (trung tâm không cần real-time, AD-8)

**Given** tôi ở Central instance
**When** tôi mở Master Data
**Then** dữ liệu hiển thị read-only, kèm link "Mở tại site X" để thao tác ghi trực tiếp tại site đó (UX-DR5, AD-4)

### Story 5.3: Hiển thị trạng thái đồng bộ (Sync Badge)

As an Admin/Manager viewing cross-site data,
I want to see "last synced X minutes ago" per site,
So that I don't mistake a quiet site for a site that's down.

**Acceptance Criteria:**

**Given** tôi xem Dashboard/Report tại Central
**When** dữ liệu của một site hiển thị
**Then** kèm badge "Đồng bộ lần cuối: X phút trước" dựa trên last-sync-timestamp của site đó (UX-DR11)

**Given** thời gian từ lần đồng bộ cuối vượt ngưỡng cấu hình
**When** badge hiển thị
**Then** chuyển màu cảnh báo — nhưng không phải lỗi, chỉ là thông tin (UX-DR11)

**Given** Admin xem badge cảnh báo
**When** điều tra
**Then** phân biệt được đây là vấn đề đồng bộ (kết nối liên site) chứ không phải máy hỏng tại site đó
