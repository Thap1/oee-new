---
name: 'oee-new'
description: 'Web app theo dõi OEE đa nhà máy. Kế thừa PrimeNG + Sakai-NG (Angular 21) làm hệ thống UI nền; DESIGN.md này chỉ định nghĩa phần brand-layer delta (màu trạng thái máy, quy mô chữ cho màn hình xưởng) trên nền Sakai.'
status: final
created: '2026-07-17'
updated: '2026-07-17'
colors:
  # Tất cả token chrome khác (background, surface, border, text-muted...) kế thừa nguyên theme PrimeOne mặc định của Sakai-NG.
  status-running: '#22C55E'
  status-running-foreground: '#FFFFFF'
  status-stopped: '#EF4444'
  status-stopped-foreground: '#FFFFFF'
  status-idle: '#F59E0B'
  status-idle-foreground: '#1A1208'
  status-no-signal: '#6B7280'
  status-no-signal-foreground: '#FFFFFF'
  loss-availability: '#EF4444'
  loss-performance: '#F59E0B'
  loss-quality: '#8B5CF6'
typography:
  # body/label/caption kế thừa font mặc định của Sakai (Inter). Chỉ override display cho số liệu lớn đọc từ xa trên xưởng.
  shopfloor-display:
    fontFamily: 'inherit'
    fontSize: 56px
    fontWeight: '700'
    lineHeight: '1.1'
  shopfloor-label:
    fontFamily: 'inherit'
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '1.3'
rounded:
  # Kế thừa nguyên bộ rounded mặc định của Sakai/PrimeOne — không override.
spacing:
  # Kế thừa thang spacing mặc định của Sakai; riêng card trạng thái máy trên dashboard xưởng dùng padding lớn hơn (xem Components).
components:
  machine-status-card:
    background: '{colors.status-running}'
    foreground: '{colors.status-running-foreground}'
    radius: '{rounded.lg}'
    padding: '24px'
    minTouchTarget: '96px'
  reason-code-button:
    radius: '{rounded.md}'
    minTouchTarget: '64px'
  loss-pie-chart:
    paletteAvailability: '{colors.loss-availability}'
    palettePerformance: '{colors.loss-performance}'
    paletteQuality: '{colors.loss-quality}'
---

# DESIGN.md — oee-new

## Brand & Style

oee-new là công cụ vận hành nội bộ, không phải sản phẩm thương mại có brand riêng — mình chủ động **kế thừa gần như toàn bộ** hệ thống thị giác của **Sakai-NG** (template admin chính thức, miễn phí của PrimeNG, đã cập nhật Angular 21), thay vì thiết kế nhận diện riêng. `[ASSUMPTION]` chưa có brand guideline nào từ nhà máy — nếu sau này có logo/màu thương hiệu công ty, chỉ cần override `colors.primary` mà không ảnh hưởng phần còn lại.

Phần brand-layer duy nhất oee-new tự định nghĩa là **ngữ nghĩa màu trạng thái máy** (running/stopped/idle/no-signal) và **quy mô chữ cho màn hình xưởng** — vì đây là ngôn ngữ thị giác đặc thù ngành sản xuất (đèn tín hiệu Andon: xanh/đỏ/vàng), không phải thứ Sakai có sẵn.

## Colors

- **Trạng thái máy** (dùng cho Machine Status Card, badge, chấm tròn trên sidebar): `status-running` (xanh lá) = máy đang chạy; `status-stopped` (đỏ) = máy dừng, cần chọn lý do; `status-idle` (vàng) = máy chạy chậm/chờ; `status-no-signal` (xám) = mất tín hiệu từ máy (khác với dừng máy — không được tô đỏ để tránh hiểu nhầm là downtime thật).
- **Nhóm tổn thất OEE** (dùng riêng cho pie chart FR-019, KHÔNG dùng lại màu trạng thái máy ở trên để tránh nhầm lẫn hai hệ ngữ nghĩa khác nhau): `loss-availability` (đỏ), `loss-performance` (vàng), `loss-quality` (tím). Ba màu này cố định xuyên suốt mọi pie chart/báo cáo trong app.
- Mọi token khác (background, surface, text, border, primary action...) **kế thừa nguyên theme PrimeOne mặc định của Sakai** — không override, giữ đúng kỷ luật "không có lý do brand thì không override".

## Typography

Body/label/caption dùng nguyên font mặc định Sakai (Inter). Riêng màn hình dashboard xưởng cần đọc được **từ xa vài mét**, nên thêm 2 role chữ lớn: `shopfloor-display` (56px, đậm — dùng cho số OEE/tên máy chính) và `shopfloor-label` (20px — nhãn phụ). Không dùng size mặc định của Sakai (vốn tối ưu cho bàn làm việc, không phải màn hình treo tường xưởng) cho hai vị trí này.

## Layout & Spacing

Kế thừa lưới/spacing mặc định của Sakai. Riêng `machine-status-card` trên **dashboard xưởng** dùng padding lớn (24px) và `minTouchTarget: 96px` — ngón tay dính dầu mỡ chạm không cần chính xác cao. Màn hình **báo cáo/master data** (dùng bàn phím chuột văn phòng) giữ nguyên spacing chuẩn của Sakai, mật độ thông tin cao hơn bình thường.

## Elevation & Depth

Kế thừa nguyên shadow/elevation mặc định của Sakai cho card, dropdown, dialog — không override.

## Shapes

Kế thừa nguyên bộ `rounded` mặc định của Sakai/PrimeOne.

## Components

Component nền (Card, Button, Dropdown, Sidebar, Topbar, DataTable, Chart) lấy nguyên từ Sakai-NG/PrimeNG — 80% giao diện không cần custom gì thêm. oee-new chỉ thêm 3 biến thể/thành phần đặc thù:

- **Machine Status Card** — biến thể của PrimeNG Card, nền đổi theo `status-*` tương ứng, chữ theo `typography.shopfloor-display`.
- **Reason Code Button** — biến thể PrimeNG Button, `minTouchTarget: 64px`, dùng trong lưới chọn lý do dừng máy.
- **Loss Pie Chart** — PrimeNG Chart (Chart.js) với palette cố định 3 màu `loss-*`, không đổi theo theme sáng/tối.

## Do's and Don'ts

- **Do** giữ nguyên mọi token/component Sakai không được liệt kê ở trên — đừng tự ý "làm đẹp thêm".
- **Do** luôn đi kèm icon/nhãn chữ cùng với màu trạng thái máy (không dùng màu như tín hiệu duy nhất).
- **Don't** dùng lại 3 màu `loss-*` (Availability/Performance/Quality) cho bất kỳ mục đích nào khác ngoài pie chart tổn thất OEE — sẽ gây nhầm lẫn ngữ nghĩa với màu trạng thái máy.
- **Don't** thu nhỏ `machine-status-card`/`reason-code-button` xuống dưới `minTouchTarget` đã định nghĩa, kể cả trên màn hình rộng — đây là ràng buộc công thái học cho môi trường xưởng, không phải thẩm mỹ.
