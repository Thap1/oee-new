---
name: 'oee-new'
status: final
created: '2026-07-17'
updated: '2026-07-17'
sources: ['../prds/prd-oee-new-2026-07-17/prd.md', '../prds/prd-oee-new-2026-07-17/addendum.md', '../architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md']
---

# EXPERIENCE.md — oee-new

## Foundation

**Form-factor:** Web, đa bề mặt (multi-surface) — hai ngữ cảnh sử dụng khác hẳn nhau trên cùng một app:
- **Màn hình xưởng** — thường là màn hình lớn/tablet gắn cạnh line, thao tác chạm, người dùng đứng cách xa vài mét, tay có thể dính dầu mỡ (Operator).
- **Màn hình văn phòng** — desktop chuẩn, bàn phím/chuột, xem báo cáo (Manager/Viewer), cấu hình master data (Admin).

**UI system:** [Sakai-NG](https://github.com/primefaces/sakai-ng) — template admin miễn phí chính thức của PrimeNG, Angular 21 (khớp `ARCHITECTURE-SPINE.md` stack). Layout kế thừa nguyên cấu trúc Sakai: topbar + sidebar menu thu gọn được + footer + vùng nội dung chính. Xem `DESIGN.md` cho token thị giác (màu trạng thái, typography màn hình xưởng).

## Information Architecture

Sidebar (theo source tree đã chốt ở architecture spine), hiển thị mục nào tuỳ vai trò:

| Mục sidebar | Operator | Manager/Viewer | Admin |
| --- | --- | --- | --- |
| Dashboard | ✅ (máy/line mình phụ trách) | ✅ (site/line được gán) | ✅ |
| Downtime (lịch sử ghi nhận) | ✅ (chỉ xem của mình) | ✅ | ✅ |
| Reports | ❌ | ✅ | ✅ |
| Master Data | ❌ | ❌ | ✅ |

**Topbar:** Site/Line selector (ẩn khi chỉ có 1 site — progressive disclosure theo AD-1 của architecture spine), chuyển ngôn ngữ Việt/Anh (runtime, không reload trang — theo `@ngx-translate/core` đã chốt), menu user.

**Central instance (Admin xem xuyên site):** cùng IA, nhưng Dashboard/Reports gộp nhiều site; Master Data ở Central là read-only (theo AD-4 — trung tâm không ghi ngược xuống site), có link "Mở tại site X" để Admin thao tác ghi trực tiếp tại site đó.

## Voice and Tone

- **Màn hình xưởng:** ngắn, mệnh lệnh, không giải thích dài dòng — Operator đang đứng máy, không có thời gian đọc. Ví dụ nhãn lý do dừng máy là danh từ ngắn ("Kẹt khuôn", "Đổi ca"), không phải câu đầy đủ.
- **Màn hình văn phòng:** đầy đủ hơn, mô tả rõ ngữ cảnh (vd. tiêu đề báo cáo có kèm khoảng thời gian, site, line).
- Thông báo lỗi luôn nêu **hành động tiếp theo**, không chỉ nêu vấn đề (vd. "Mất tín hiệu máy X — kiểm tra kết nối" thay vì "Lỗi").

## Component Patterns

- **Machine Status Card** (`DESIGN.md.components.machine-status-card`): nền đổi màu theo trạng thái, luôn có icon + nhãn chữ đi kèm màu (Accessibility Floor). Chạm vào card khi máy đang `Stopped` → mở Reason Code Picker.
- **Reason Code Picker:** lưới nút lớn (`DESIGN.md.components.reason-code-button`), nhóm theo `LossCategory` (Availability/Performance/Quality — khớp AD-5 architecture), chạm 1 lần để chọn, tự đóng sau khi chọn — không có bước "Xác nhận" phụ (giảm thao tác cho Operator).
- **Loss Pie Chart** (`DESIGN.md.components.loss-pie-chart`, FR-019..021): PrimeNG Chart (Chart.js), có dropdown lọc theo Equipment hoặc Production Area, date-picker chọn ngày cụ thể để drill-down. Palette 3 màu cố định theo `DESIGN.md.colors.loss-*`, không đổi theo theme sáng/tối.
- **Site/Line Selector:** dropdown ở topbar, chỉ hiện khi user có quyền trên >1 site/line.
- **Sync/Last-updated Badge** (Manager/Admin xem báo cáo xuyên site): hiển thị "Đồng bộ lần cuối: X phút trước" theo site — hệ quả bắt buộc từ observability tối thiểu đã chốt ở architecture spine, để không hiểu nhầm site im lặng là site ngừng hoạt động.

## State Patterns

- **Loading:** skeleton card khi dashboard đang chờ dữ liệu real-time đầu tiên từ SignalR.
- **Real-time update:** card có hiệu ứng nhấp nháy nhẹ (pulse, không giật) khi trạng thái máy đổi — báo hiệu "vừa cập nhật" mà không gây giật mình trên màn hình lớn.
- **No signal:** card màu xám (`status-no-signal`) + icon mất kết nối + nhãn "Mất tín hiệu Xp" — tách biệt rõ với `status-stopped` (đỏ) để Operator/Manager không nhầm mất tín hiệu thành máy dừng thật (FR-003).
- **Empty:** chưa có máy nào được gán cho user này — hướng dẫn liên hệ Admin, không để trắng trơn.
- **Sync-lag (Central/Manager multi-site):** badge "Đồng bộ lần cuối" chuyển sang màu cảnh báo nếu quá lâu chưa đồng bộ — không phải lỗi, chỉ là thông tin (site vẫn tự vận hành bình thường).

## Interaction Primitives

- **Chạm trước (touch-first)** trên màn hình xưởng — không có thao tác nào yêu cầu double-tap, drag, hay gõ chữ để hoàn thành ghi nhận downtime.
- **Cập nhật đẩy (push), không kéo (pull)** — dashboard không có nút "Refresh" thủ công; dữ liệu tự cập nhật qua SignalR.
- **Drill-down bằng chạm/click trực tiếp vào lát pie chart** — không cần menu phụ để xem chi tiết theo Equipment/Area/ngày.

## Accessibility Floor

- Màu **không bao giờ là tín hiệu duy nhất** — mọi trạng thái máy có icon + nhãn chữ đi kèm màu (đáp ứng người mù màu, phổ biến trong môi trường công nghiệp).
- Kích thước chạm tối thiểu trên màn hình xưởng: `96px` cho status card, `64px` cho reason-code button (`DESIGN.md.components`) — lớn hơn hẳn chuẩn web thông thường (44px) do điều kiện thao tác thực tế (tay dính dầu, đứng xa).
- Độ tương phản màu trạng thái phải đạt tối thiểu AA trên cả theme sáng lẫn tối của Sakai.
- Chữ số OEE chính trên dashboard xưởng dùng `shopfloor-display` (56px) — đọc được từ khoảng cách vài mét.

## Key Flows

**UJ-1 — Operator ghi nhận downtime** (kế thừa từ PRD, bổ sung chi tiết UX): Anh Bình đứng cạnh line, máy dừng đột ngột → Machine Status Card chuyển đỏ + rung nhẹ (pulse) trong dashboard xưởng. Anh chạm vào card → Reason Code Picker mở full-screen, hiện lưới nút lớn theo nhóm màu loss category. **Climax:** anh chạm đúng 1 lần vào "Kẹt khuôn" → picker tự đóng, card trở lại xanh khi máy chạy lại, không có bước xác nhận nào chặn anh quay lại công việc.

**UJ-2 — Manager xem báo cáo cuối ca** (kế thừa từ PRD): chị Hoa mở Reports trên desktop văn phòng, chọn site/line qua Site Selector ở topbar → bảng + pie chart theo loss category hiện ra, badge "Đồng bộ lần cuối" cho biết dữ liệu có mới nhất chưa. **Climax:** chị chạm vào lát đỏ (Availability loss) trong pie chart → drill-down thấy ngay mã lý do dừng máy chiếm nhiều thời gian nhất trong ca, đưa thẳng vào ghi chú họp giao ca mà không cần mở Excel.

**UJ-3 — Admin xử lý sự cố mất tín hiệu máy** *(mới, phát sinh từ FR-003):* chị Lan (Admin) thấy một Machine Status Card chuyển xám ở dashboard, nhãn "Mất tín hiệu 15p" — khác hẳn màu đỏ nên chị biết ngay đây là vấn đề kết nối, không phải máy hỏng, và điều hướng xử lý đúng (gọi IT thay vì gọi bảo trì cơ khí).
