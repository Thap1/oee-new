---
title: PRD - Web App OEE
status: final
created: 2026-07-17
updated: 2026-07-17
---

# PRD — Web App theo dõi OEE (Overall Equipment Effectiveness)

## 1. Tổng quan & Tầm nhìn

**Bối cảnh:** Nhà máy hiện chưa có công cụ số hoá để theo dõi hiệu suất thiết bị (OEE = Availability × Performance × Quality) theo thời gian thực. Việc ghi nhận downtime, sản lượng, và phế phẩm đang phụ thuộc vào quy trình thủ công/rời rạc, khiến quản lý khó nhìn thấy bức tranh vận hành tức thời và khó truy vết nguyên nhân gốc của tổn thất hiệu suất.

**Tầm nhìn:** Xây dựng một web app nội bộ, đa nhà máy (multi-site), cho phép:
- Người vận hành trên xưởng thấy tình trạng máy của mình theo thời gian thực và ghi nhận lý do dừng máy nhanh chóng.
- Quản lý xem báo cáo OEE tổng hợp theo ca/ngày/tuần trên toàn bộ các nhà máy được phân quyền.
- Quản trị viên cấu hình danh mục máy, ca làm việc, người dùng và mã lý do dừng máy một cách tập trung.

**Ngoài phạm vi (giai đoạn này):** tích hợp trực tiếp giao thức PLC/SCADA thực tế (xem mục 7 — Giả định), cảnh báo tự động, module quản lý chất lượng/phế phẩm chi tiết (root-cause, SPC...).

## 2. Người dùng & Vai trò

Hệ thống có 4 vai trò, giới hạn quyền theo Site/Line được gán (trừ Admin):

| Vai trò | Mô tả | Phạm vi |
|---|---|---|
| **Admin** | Toàn quyền: cấu hình máy, line, ca, người dùng, mã lý do dừng máy | Toàn bộ các site |
| **Manager** | Xem/duyệt báo cáo OEE, không sửa master data | Site/Line được gán |
| **Operator** | Ghi nhận lý do dừng máy, xem dashboard máy mình phụ trách | Line/ca được gán |
| **Viewer** | Chỉ xem dashboard/báo cáo, không thao tác | Site/Line được gán |

## 3. Hành trình người dùng chính

**UJ-1 — Operator ghi nhận downtime (Anh Bình, vận hành line ép nhựa ca sáng):**
Anh Bình đứng trước màn hình lớn cạnh line sản xuất. Máy đột ngột dừng — dashboard chuyển đỏ ngay lập tức. Anh chạm vào máy đang dừng, một danh sách mã lý do dừng máy hiện ra (đã phân nhóm sẵn theo Availability/Performance/Quality loss), anh chọn đúng lý do trong vài giây rồi quay lại công việc — không cần gõ chữ, không rời vị trí đứng máy.

**UJ-2 — Manager xem báo cáo cuối ca (Chị Hoa, quản lý sản xuất phụ trách 3 nhà máy):**
Cuối ca chiều, chị Hoa mở báo cáo tổng hợp trên máy tính văn phòng, lọc theo từng nhà máy chị phụ trách, thấy ngay line nào có OEE thấp nhất trong ca và lý do dừng máy chiếm nhiều thời gian nhất — để đưa vào cuộc họp giao ca mà không cần tự tổng hợp Excel thủ công.

## 4. Tính năng & Yêu cầu chức năng

### 4.1 Thu thập dữ liệu sản xuất (Ingestion)

- **FR-001:** Hệ thống phải nhận dữ liệu sản lượng/trạng thái máy từ các máy trong xưởng một cách tự động (không cần nhập tay), theo cấu hình từng máy.
- **FR-002:** Hệ thống phải chuẩn hoá dữ liệu nhận vào bất kể máy sử dụng phương thức kết nối gì, để không phải thay đổi phần tính toán OEE khi thêm loại máy mới. `[ASSUMPTION]` giai đoạn đầu, một số máy có thể chưa kết nối tự động được — hệ thống cho phép nhập bù thủ công cho các máy này như phương án dự phòng.
- **FR-003:** Hệ thống phải phát hiện và cảnh báo (ở mức hiển thị) khi một máy ngừng gửi dữ liệu quá thời gian quy định (mất kết nối), để tránh hiểu nhầm "máy đang chạy tốt" khi thực ra là mất tín hiệu.

### 4.2 Dashboard OEE thời gian thực

- **FR-004:** Operator phải xem được trạng thái và chỉ số OEE của (các) máy mình phụ trách theo thời gian thực, cập nhật trong vài giây khi trạng thái máy thay đổi.
- **FR-005:** Dashboard phải thể hiện trạng thái máy bằng màu sắc rõ ràng (chạy/dừng/cảnh báo), đọc được từ khoảng cách xa trên xưởng.
- **FR-006:** Manager/Viewer phải xem được dashboard tổng hợp nhiều máy/line/site cùng lúc, giới hạn theo site/line được phân quyền.
- **FR-007:** Giao diện phải hỗ trợ song ngữ Việt/Anh, chuyển đổi được theo lựa chọn người dùng.
- **FR-019:** Dashboard phải cung cấp biểu đồ dạng **pie chart** thể hiện tỷ lệ phân bổ tổn thất/thời gian. `[ASSUMPTION]` pie chart thể hiện tỷ lệ theo 3 loại tổn thất OEE (Availability/Performance/Quality loss) và/hoặc theo từng mã lý do dừng máy — cần bạn xác nhận nội dung chính xác của biểu đồ.
- **FR-020:** Người dùng phải chọn xem pie chart theo **Equipment** (máy cụ thể) hoặc theo **Production Area** (khu vực sản xuất/line), trong phạm vi site/line được phân quyền.
- **FR-021:** Người dùng phải xem được **chi tiết theo ngày** (drill-down chọn một ngày cụ thể để xem lại phân bổ pie chart của ngày đó), không chỉ xem tổng hợp nhiều ngày gộp lại.

### 4.3 Ghi nhận downtime & mã lý do

- **FR-008:** Khi máy dừng, Operator phải chọn lý do dừng máy từ một danh mục mã lý do chuẩn hoá (không phải nhập tự do).
- **FR-009:** Mỗi mã lý do dừng máy phải được phân nhóm vào đúng một trong ba loại tổn thất OEE — Availability loss / Performance loss / Quality loss — để báo cáo tự động quy về đúng thành phần công thức.
- **FR-010:** Hệ thống phải ghi nhận số lượng sản phẩm lỗi/phế phẩm cơ bản (đủ để tính thành phần Quality của OEE). `[ASSUMPTION]` module quản lý chất lượng chi tiết (phân loại lỗi sâu, truy vết nguyên nhân) không thuộc phạm vi MVP.

### 4.4 Master Data & Phân quyền

- **FR-011:** Admin phải quản lý (thêm/sửa/xoá) được danh mục Site, Line, Machine theo cấu trúc phân cấp Site > Line > Machine.
- **FR-012:** Admin phải quản lý được ca làm việc (shift schedule) áp dụng cho từng site/line.
- **FR-013:** Admin phải quản lý được người dùng và gán vai trò (Admin/Manager/Operator/Viewer) kèm phạm vi site/line tương ứng.
- **FR-014:** Admin phải quản lý được danh mục mã lý do dừng máy và nhóm tổn thất tương ứng (liên kết FR-009).
- **FR-015:** Hệ thống phải từ chối thao tác ngoài phạm vi site/line được phân quyền của người dùng, ở cả giao diện lẫn tầng xử lý phía sau.

### 4.5 Báo cáo

- **FR-016:** Manager/Viewer phải xem được báo cáo OEE tổng hợp theo ca, theo ngày, và theo tuần.
- **FR-017:** Báo cáo phải cho phép lọc theo site/line/máy trong phạm vi được phân quyền.
- **FR-018:** Báo cáo phải hiển thị được nguyên nhân dừng máy chiếm nhiều thời gian nhất trong kỳ báo cáo, để hỗ trợ họp giao ca.

## 5. Yêu cầu phi chức năng (NFR)

- **NFR-1 (Real-time):** Thay đổi trạng thái máy phải phản ánh lên dashboard trong vòng vài giây, không yêu cầu người dùng tải lại trang.
- **NFR-2 (Multi-site):** Hệ thống phải hỗ trợ nhiều nhà máy hoạt động đồng thời ngay từ ngày ra mắt, với khả năng xem tổng hợp toàn hệ thống cho Admin.
- **NFR-3 (Triển khai on-premise):** Mỗi site có hạ tầng (server/DB) on-premise riêng, hoạt động độc lập không phụ thuộc kết nối liên site. Dữ liệu được đồng bộ định kỳ về một điểm tổng hợp để Admin/Manager xem toàn hệ thống multi-site.
- **NFR-4 (Chịu lỗi mạng xưởng):** Việc ghi nhận lý do dừng máy tại xưởng không được phụ thuộc hoàn toàn vào kết nối ra ngoài site, để không gây gián đoạn thao tác của Operator khi mạng chập chờn.
- **NFR-5 (Phân quyền):** Mọi API/màn hình phải thực thi kiểm soát truy cập theo vai trò và phạm vi site/line — không chỉ ẩn/hiện ở giao diện.
- **NFR-6 (Song ngữ):** Toàn bộ giao diện người dùng phải hỗ trợ tiếng Việt và tiếng Anh.

## 6. Chỉ số thành công

- `[ASSUMPTION]` **Chỉ số chính:** % thời gian dừng máy được ghi nhận có lý do rõ ràng (so với hiện tại phần lớn ghi chép thủ công/thiếu) — mục tiêu đề xuất ≥ 90% trong vòng 1 tháng sau triển khai mỗi site.
- `[ASSUMPTION]` **Chỉ số phụ:** Thời gian quản lý tổng hợp báo cáo cuối ca giảm đáng kể so với quy trình Excel thủ công hiện tại.
- **Counter-metric (giới hạn rủi ro):** Thời gian Operator dành để nhập lý do dừng máy không được tăng thêm đáng kể so với quy trình hiện tại — nếu nhập liệu quá rườm rà, dữ liệu sẽ bị nhập ẩu để "cho xong", làm hỏng chất lượng số liệu OEE.

*(Ba chỉ số trên là đề xuất ban đầu của mình dựa trên bối cảnh — cần bạn xác nhận hoặc thay bằng số liệu/mục tiêu thực tế của nhà máy.)*

## 7. Giả định & Câu hỏi mở

- `[ASSUMPTION]` Chưa tích hợp giao thức máy thật (OPC-UA/MQTT/Modbus...) ở giai đoạn này — xem quyết định kiến trúc Ingestion Adapter trong `addendum.md`.
- `[OPEN QUESTION]` Timeline mong muốn và site nào triển khai thí điểm đầu tiên chưa được xác định — không chặn PRD, cần chốt trước Sprint Planning.
- `[ASSUMPTION]` Cảnh báo tự động (alert khi OEE thấp) và module chất lượng/phế phẩm chi tiết được xếp vào backlog sau MVP, không chặn PRD này.

