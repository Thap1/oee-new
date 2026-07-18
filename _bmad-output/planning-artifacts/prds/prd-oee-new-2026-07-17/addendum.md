# Addendum — Ghi chú kỹ thuật & quyết định kiến trúc (oee-new)

Tài liệu này lưu các quyết định/thảo luận kỹ thuật đã chốt trong phiên brainstorm + party-mode ngày 2026-07-17, không đưa vào PRD chính vì PRD tập trung vào capability, không phải implementation. Đây là input cho `bmad-architecture` sau này.

## Stack đã chọn

- **DB:** PostgreSQL
- **BE:** .NET (ASP.NET Core Web API)
- **FE:** Angular + **PrimeNG** (thư viện UI component)
- Tách biệt rõ 3 lớp DB / BE / FE theo yêu cầu ban đầu của user.

## Quyết định: Ingestion Adapter Pattern

Không tích hợp giao thức PLC thật (OPC-UA/MQTT/Modbus...) ở MVP vì:
- Chưa biết máy nào, giao thức gì.
- Tích hợp phần cứng thực tế là dự án OT riêng (điện, tự động hoá, đàm phán vendor), có thể mất vài tháng.

Giải pháp: định nghĩa interface `IProductionDataSource` trong domain, cùng một endpoint ingestion nhận dữ liệu đã chuẩn hoá (JSON: machine_id, timestamp, counter, status). Khi biết máy thật, viết adapter riêng (OPC-UA client, MQTT subscriber, script đọc SCADA/MES...) đẩy vào cùng endpoint/queue — không đổi domain logic hay DB schema.

## Quyết định: Phân cấp dữ liệu Site > Line > Machine

Model hierarchy này trong DB ngay từ ngày một (rẻ khi làm sớm, tốn kém nếu retrofit sau khi đã có data thật + FK + báo cáo phụ thuộc). Tham chiếu ISO 22400 (chuẩn KPI sản xuất): Site → Area → Work Center/Line → Machine.

UI dùng progressive disclosure: ẩn site-selector khi chỉ có 1 site hiển thị, hiện ra khi có nhiều site (áp dụng ngay vì MVP đã multi-site).

## Quyết định: RBAC 4 role

Admin / Manager / Operator / Viewer, gắn permission theo site/line ngay trong role model (không chỉ ẩn/hiện UI — enforce ở tầng API).

## Quyết định: Reason code chuẩn hoá

Downtime reason code là danh mục quản lý được (không free-text), nhóm theo Availability Loss / Performance Loss / Quality Loss để khớp trực tiếp 3 thành phần công thức OEE trong tính toán báo cáo tự động.

## Quyết định: Đồng bộ dữ liệu multi-site on-premise

Mỗi site có server/DB on-premise riêng, hoạt động độc lập (không phụ thuộc kết nối liên site để vận hành hàng ngày — đáp ứng NFR-4 chịu lỗi mạng). Dữ liệu đồng bộ định kỳ về một điểm tổng hợp trung tâm để Admin/Manager xem toàn hệ thống multi-site (đáp ứng NFR-3). Chi tiết cơ chế đồng bộ (tần suất, giao thức, xử lý xung đột dữ liệu...) để `bmad-architecture` quyết định cụ thể.

## Câu hỏi mở cho giai đoạn Architecture

- **Ingestion layer đầu tiên:** trước khi có adapter thật, cần quyết định nguồn tạm — nhập tay qua form, hay giả lập từ file/script — để dev + test dashboard real-time không bị chặn bởi việc chưa có máy thật kết nối.
