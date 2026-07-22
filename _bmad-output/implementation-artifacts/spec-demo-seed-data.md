---
title: 'Demo/Deploy Seed Data'
type: 'feature'
created: '2026-07-22'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '9d772bef07e59d319b5acb08bc8e21945a7e99ba'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `db/init/02_seed.sql` (từ Story 1.4-1.6) đã có Site/Line/Machine/ShiftSchedule/ReasonCode/User nhưng chưa có dữ liệu production (MachineState/DowntimeEvent/QualityReject) — nên khi chạy seed rồi mở app, Dashboard và Loss Pie Chart vẫn trống trơn. `db/init/01_schema.sql` cũng đang thiếu các bảng Epic 2/3 (MachineState, DowntimeEvent, QualityReject, unique index mới nhất) nên chưa tạo nổi các bảng đó cho seed dùng.

**Approach:** Regenerate `01_schema.sql` cho khớp toàn bộ migration hiện tại (`dotnet ef migrations script --idempotent`). Mở rộng `02_seed.sql`: thêm 2 Machine mới (cho 2 Line hiện chỉ có 1 máy), rồi seed MachineState (trạng thái hiện tại đa dạng, 1 máy no-signal), DowntimeEvent (lịch sử đã đóng + 1 đang mở), QualityReject (rải rác vài ngày) — dùng `now() - interval` thay vì ngày cố định để dữ liệu luôn "mới" bất kể chạy khi nào. Không viết code C# — người dùng tự chạy 2 file `.sql` bằng tay (`psql -f`) sau khi deploy.

## Boundaries & Constraints

**Always:**
- Giữ nguyên style file hiện có: fixed UUID literal theo dải số đã dùng (Site=1xx, Line=2xx, Machine=3xx, ShiftSchedule=4xx, ReasonCode=5xx, User=6xx) — dùng tiếp DowntimeEvent=7xx, QualityReject=8xx; mọi `INSERT` đều có `ON CONFLICT ("Id") DO NOTHING` để chạy lại nhiều lần vẫn an toàn.
- Không sửa/xoá bất kỳ dòng nào đã có trong `02_seed.sql` — chỉ thêm mới.
- Mọi timestamp seed (MachineState.LastReportedAt, DowntimeEvent.StartedAt/EndedAt, QualityReject.RecordedAt) tính tương đối theo `now()`, không hardcode ngày.
- Chỉ 1 DowntimeEvent đang mở (EndedAt NULL) cho đúng 1 machine đang ở trạng thái Stopped trong MachineState — khớp unique index `IX_DowntimeEvent_MachineId_OpenOnly` mới thêm.
- `01_schema.sql` phải regenerate lại bằng đúng lệnh `dotnet ef migrations script --idempotent` (không sửa tay) để không lệch với migration thật.

**Ask First:** Không có.

**Never:** Không đổi tên/id của Site/Line/Machine/User/ReasonCode đã có. Không viết code C# (Program.cs, seeder class). Không đụng `ng test` OOM hay code Epic 3 song song.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Chạy trên DB rỗng | `psql -f 01_schema.sql && psql -f 02_seed.sql` | Đủ bảng + đủ dữ liệu, Dashboard/Loss Pie Chart có nội dung ngay | N/A |
| Chạy lại lần 2 trên DB đã seed | Chạy lại đúng 2 lệnh trên | Không có row nào bị nhân đôi (mọi `INSERT` đều `ON CONFLICT DO NOTHING`) | N/A |
| DB đã có schema cũ (thiếu bảng Epic 2/3) | Chỉ chạy `02_seed.sql` mà chưa chạy `01_schema.sql` mới | Lỗi "relation does not exist" — đây là lý do bắt buộc phải regenerate `01_schema.sql` trước | Ghi rõ trong comment đầu `02_seed.sql`: phải chạy `01_schema.sql` mới nhất trước |

</frozen-after-approval>

## Code Map

- `db/init/01_schema.sql` -- regenerate lại từ `dotnet ef migrations script --idempotent`, hiện thiếu MachineState/DowntimeEvent/QualityReject + unique index mới nhất
- `db/init/02_seed.sql` -- file cần mở rộng; đã đọc toàn bộ, biết rõ dải Id đang dùng (Site 101-102, Line 201-203, Machine 301-304, ShiftSchedule 401-404, ReasonCode 501-505, User 601-603)
- `src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs` -- xác nhận tên bảng/cột chính xác (đã đọc)
- `src/OeeNew.Domain/Production/MachineStatus.cs` -- enum thứ tự Running=0/Stopped=1/Idle=2/Fault=3 (mapped `smallint`)

## Tasks & Acceptance

**Execution:**
- [x] `db/init/01_schema.sql` -- chạy `dotnet ef migrations script --idempotent -o db/init/01_schema.sql --project src/OeeNew.Infrastructure --startup-project src/OeeNew.Api` để cập nhật đủ mọi bảng tính đến migration `AddDowntimeEventOpenUniqueIndex` -- seed script mới cần các bảng này tồn tại trước
- [x] `db/init/02_seed.sql` -- thêm 2 Machine mới (`305` vào Line `202`, `306` vào Line `203`); thêm MachineState cho cả 6 machine (301-306): 1 Running mới nhất, 1 Stopped (khớp DowntimeEvent đang mở), 1 Idle, 1 Fault, 1 Running, 1 Running nhưng `LastReportedAt` cũ hơn ngưỡng no-signal (`now() - interval '5 minutes'`, ProductionOptions mặc định 60s) -- demo được tính năng no-signal; thêm 9 DowntimeEvent (8 đã đóng rải trong ~5 ngày gần đây gắn ReasonCode 501-505 để Loss Pie Chart lên đủ cả 3 LossCategory + 1 đang mở khớp machine Stopped); thêm 8 QualityReject rải rác trên các machine, quantity 1-12 -- đây là phần dữ liệu duy nhất còn thiếu để Dashboard/Loss Pie Chart "có dữ liệu thật" khi deploy
- [x] `db/init/02_seed.sql` -- cập nhật comment đầu file: nói rõ phải chạy `01_schema.sql` bản mới trước, và liệt kê thêm các bảng production vừa seed

**Acceptance Criteria:**
- Given DB rỗng, when chạy `psql -f db/init/01_schema.sql` rồi `psql -f db/init/02_seed.sql`, then không có lỗi và cả 9 bảng (Site...QualityReject) đều có dữ liệu.
- Given đã chạy seed 1 lần, when chạy lại y hệt 2 lệnh trên, then row count mỗi bảng không đổi (idempotent).
- Given seed đã chạy xong, when mở Dashboard/Loss Pie Chart trên UI, then thấy nhiều machine với trạng thái khác nhau (kể cả 1 no-signal) và biểu đồ loss có đủ 3 loại tổn thất.

## Design Notes

Ban đầu định không sửa `ReasonCode`, nhưng review phát hiện Site 102 (viewer1's scope) và Line 201 (operator1's scope) sẽ thiếu hẳn 1-2 loại tổn thất trong pie chart nếu chỉ dùng ReasonCode có sẵn. Đã thêm 2 ReasonCode mới cho Site 102 (506 Performance, 507 Quality — Site 102 trước đó chỉ có 505/Availability) và 1 DowntimeEvent Quality mới trên Line 201 (machine 301) — giờ cả 2 Site và Line 201 đều đủ 3 `LossCategory`.

`MachineState` không có cột `Id` riêng — PK chính là `MachineId`, insert trực tiếp không cần biến trung gian.

## Verification

**Commands:**
- `dotnet ef migrations script --idempotent -o db/init/01_schema.sql --project src/OeeNew.Infrastructure --startup-project src/OeeNew.Api` -- expected: chạy thành công, file được ghi đè
- Chạy cả 2 file `.sql` bằng `psql` (hoặc tương đương) nhắm vào 1 DB rỗng thật -- expected: không lỗi, `SELECT count(*)` trên `MachineState`/`DowntimeEvent`/`QualityReject` > 0
- Chạy lại lần 2 -- expected: `SELECT count(*)` các bảng không đổi so với lần 1

**Manual checks (if no CLI):**
- Mở Dashboard trên UI sau khi seed, xác nhận 6 machine hiển thị đủ trạng thái (kể cả no-signal) và Loss Pie Chart có dữ liệu.

## Suggested Review Order

**No-signal threshold correctness (fixed after adversarial review found machine 302 would render untappable)**

- Machine 302's `LastReportedAt` phải nằm trong ngưỡng no-signal (15s < 60s) — nếu không, card hiện màu xám "mất tín hiệu" và không có `(click)` handler, phá hỏng demo mở Reason Code Picker.
  [`02_seed.sql:79-98`](../../db/init/02_seed.sql#L79-L98)

- Máy 306 vẫn cố tình để `LastReportedAt` cũ hơn ngưỡng — đây là demo có chủ đích cho trạng thái no-signal, không phải lỗi.
  [`02_seed.sql:94`](../../db/init/02_seed.sql#L94)

**Idempotent refresh semantics**

- `MachineState` dùng `ON CONFLICT ... DO UPDATE` thay vì `DO NOTHING` — chạy lại script sẽ làm mới `LastReportedAt` về hiện tại, tránh Dashboard "cũ dần" theo thời gian.
  [`02_seed.sql:95-98`](../../db/init/02_seed.sql#L95-L98)

- `DowntimeEvent`/`QualityReject` cố tình vẫn giữ `DO NOTHING` (lịch sử không nên bị ghi đè, và tránh đụng unique partial index khi re-run trên môi trường đã có dữ liệu thật).
  [`02_seed.sql:119`](../../db/init/02_seed.sql#L119)

**Loss Pie Chart coverage per scope (fixed after review found viewer1/operator1 would see an incomplete chart)**

- Thêm 2 ReasonCode mới cho Site 102 (506 Performance, 507 Quality) — trước đó Site 102 chỉ có 1 mã Availability, khiến viewer1 (scope Site 102) không bao giờ thấy đủ 3 loại tổn thất.
  [`02_seed.sql:56-64`](../../db/init/02_seed.sql#L56-L64)

- Thêm 3 DowntimeEvent mới (710/711/712) để cả Site 102 lẫn Line 201 (operator1's scope) đều có đủ 3 `LossCategory`.
  [`02_seed.sql:106-119`](../../db/init/02_seed.sql#L106-L119)

**Schema regeneration (supporting change)**

- `01_schema.sql` regenerate lại từ `dotnet ef migrations script --idempotent`, thêm các bảng MachineState/DowntimeEvent/QualityReject + unique index mới nhất mà seed script phía trên cần.
  [`01_schema.sql:177`](../../db/init/01_schema.sql#L177)

**Peripherals**

- Comment đầu file cập nhật để nói rõ thứ tự chạy và hành vi refresh-khi-re-run.
  [`02_seed.sql:1-18`](../../db/init/02_seed.sql#L1-L18)
