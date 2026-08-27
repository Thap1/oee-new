-- OeeNew — sample seed data for a fresh environment.
-- Run AFTER 01_schema.sql (must be the current version — it now includes MachineState/
-- DowntimeEvent/QualityReject; an older 01_schema.sql will make the inserts below fail with
-- "relation does not exist"). Safe to re-run (ON CONFLICT DO NOTHING on fixed ids).
--
-- 5 Sites (Hà Nội, Đà Nẵng, Hồ Chí Minh, Hải Phòng, Cần Thơ), 15 Lines, 30 Machines — a
-- wide enough spread to demo cross-site scoping (Manager/Operator/Viewer per Site) and the
-- Central cross-site dashboard at a size that actually looks like a multi-plant operation.
--
-- Sample user passwords (for local/demo use only — rotate before any shared environment):
--   managerN (N=1..5)            / admin
--   operatorN (N=1..10)          / admin
--   viewerN (N=1..5)             / admin
--   admin                        / ChangeMe123!
-- (Admin login uses the config-driven bootstrap admin — see appsettings "BootstrapAdmin" — not a User row.)
--
-- ReasonCode is deliberately diversified per Site (7 distinct reason types drawn from a shared
-- 12-type library spanning all 3 LossCategory values) rather than repeating the same 3-4 names
-- everywhere, so the Loss Pie Chart has real variety to show off in a demo.
--
-- Also seeds production data (MachineState/DowntimeEvent/QualityReject) so the Dashboard and Loss
-- Pie Chart aren't empty right after deploy. All timestamps are relative to now() rather than a
-- fixed date. MachineState (the Dashboard's "current status") refreshes on every re-run — just run
-- this script again before a demo if it's been a while. DowntimeEvent/QualityReject history is
-- fixed at first insert (like the master data above) and simply recedes further into the past —
-- re-run this whole script on an empty DB to get fresh-looking history too.

BEGIN;

-- Sites
INSERT INTO "Site" ("Id", "Name") VALUES
    ('00000000-0000-0000-0000-000000000101', 'Nhà máy Hà Nội'),
    ('00000000-0000-0000-0000-000000000102', 'Nhà máy Đà Nẵng'),
    ('00000000-0000-0000-0000-000000000103', 'Nhà máy Hồ Chí Minh'),
    ('00000000-0000-0000-0000-000000000104', 'Nhà máy Hải Phòng'),
    ('00000000-0000-0000-0000-000000000105', 'Nhà máy Cần Thơ')
ON CONFLICT ("Id") DO NOTHING;

-- Lines
INSERT INTO "Line" ("Id", "Name", "SiteId") VALUES
    ('00000000-0000-0000-0000-000000000201', 'Line 1', '00000000-0000-0000-0000-000000000101'),
    ('00000000-0000-0000-0000-000000000202', 'Line 2', '00000000-0000-0000-0000-000000000101'),
    ('00000000-0000-0000-0000-000000000203', 'Line 3', '00000000-0000-0000-0000-000000000101'),
    ('00000000-0000-0000-0000-000000000204', 'Line 1', '00000000-0000-0000-0000-000000000102'),
    ('00000000-0000-0000-0000-000000000205', 'Line 2', '00000000-0000-0000-0000-000000000102'),
    ('00000000-0000-0000-0000-000000000206', 'Line 3', '00000000-0000-0000-0000-000000000102'),
    ('00000000-0000-0000-0000-000000000207', 'Line 1', '00000000-0000-0000-0000-000000000103'),
    ('00000000-0000-0000-0000-000000000208', 'Line 2', '00000000-0000-0000-0000-000000000103'),
    ('00000000-0000-0000-0000-000000000209', 'Line 3', '00000000-0000-0000-0000-000000000103'),
    ('00000000-0000-0000-0000-000000000210', 'Line 1', '00000000-0000-0000-0000-000000000104'),
    ('00000000-0000-0000-0000-000000000211', 'Line 2', '00000000-0000-0000-0000-000000000104'),
    ('00000000-0000-0000-0000-000000000212', 'Line 3', '00000000-0000-0000-0000-000000000104'),
    ('00000000-0000-0000-0000-000000000213', 'Line 1', '00000000-0000-0000-0000-000000000105'),
    ('00000000-0000-0000-0000-000000000214', 'Line 2', '00000000-0000-0000-0000-000000000105'),
    ('00000000-0000-0000-0000-000000000215', 'Line 3', '00000000-0000-0000-0000-000000000105')
ON CONFLICT ("Id") DO NOTHING;

-- Machines
INSERT INTO "Machine" ("Id", "Name", "LineId") VALUES
    ('00000000-0000-0000-0000-000000000301', 'Máy ép nhựa 01', '00000000-0000-0000-0000-000000000201'),
    ('00000000-0000-0000-0000-000000000302', 'Máy ép nhựa 02', '00000000-0000-0000-0000-000000000201'),
    ('00000000-0000-0000-0000-000000000303', 'Máy đóng gói 01', '00000000-0000-0000-0000-000000000202'),
    ('00000000-0000-0000-0000-000000000304', 'Máy in nhãn 01', '00000000-0000-0000-0000-000000000202'),
    ('00000000-0000-0000-0000-000000000305', 'Máy cắt biên 01', '00000000-0000-0000-0000-000000000203'),
    ('00000000-0000-0000-0000-000000000306', 'Máy đóng gói 02', '00000000-0000-0000-0000-000000000203'),
    ('00000000-0000-0000-0000-000000000307', 'Máy dệt 01', '00000000-0000-0000-0000-000000000204'),
    ('00000000-0000-0000-0000-000000000308', 'Máy dệt 02', '00000000-0000-0000-0000-000000000204'),
    ('00000000-0000-0000-0000-000000000309', 'Máy nhuộm 01', '00000000-0000-0000-0000-000000000205'),
    ('00000000-0000-0000-0000-000000000310', 'Máy sấy 01', '00000000-0000-0000-0000-000000000205'),
    ('00000000-0000-0000-0000-000000000311', 'Máy dệt 03', '00000000-0000-0000-0000-000000000206'),
    ('00000000-0000-0000-0000-000000000312', 'Máy sấy 02', '00000000-0000-0000-0000-000000000206'),
    ('00000000-0000-0000-0000-000000000313', 'Máy ép nhựa 03', '00000000-0000-0000-0000-000000000207'),
    ('00000000-0000-0000-0000-000000000314', 'Máy đóng gói 03', '00000000-0000-0000-0000-000000000207'),
    ('00000000-0000-0000-0000-000000000315', 'Máy hàn 01', '00000000-0000-0000-0000-000000000208'),
    ('00000000-0000-0000-0000-000000000316', 'Máy kiểm tra 01', '00000000-0000-0000-0000-000000000208'),
    ('00000000-0000-0000-0000-000000000317', 'Máy hàn 02', '00000000-0000-0000-0000-000000000209'),
    ('00000000-0000-0000-0000-000000000318', 'Máy kiểm tra 02', '00000000-0000-0000-0000-000000000209'),
    ('00000000-0000-0000-0000-000000000319', 'Máy đúc 01', '00000000-0000-0000-0000-000000000210'),
    ('00000000-0000-0000-0000-000000000320', 'Máy dập 01', '00000000-0000-0000-0000-000000000210'),
    ('00000000-0000-0000-0000-000000000321', 'Máy trộn 01', '00000000-0000-0000-0000-000000000211'),
    ('00000000-0000-0000-0000-000000000322', 'Máy hàn 03', '00000000-0000-0000-0000-000000000211'),
    ('00000000-0000-0000-0000-000000000323', 'Máy cắt biên 02', '00000000-0000-0000-0000-000000000212'),
    ('00000000-0000-0000-0000-000000000324', 'Máy kiểm tra 03', '00000000-0000-0000-0000-000000000212'),
    ('00000000-0000-0000-0000-000000000325', 'Máy chiết rót 01', '00000000-0000-0000-0000-000000000213'),
    ('00000000-0000-0000-0000-000000000326', 'Máy đóng thùng 01', '00000000-0000-0000-0000-000000000213'),
    ('00000000-0000-0000-0000-000000000327', 'Máy trộn 02', '00000000-0000-0000-0000-000000000214'),
    ('00000000-0000-0000-0000-000000000328', 'Máy sấy 03', '00000000-0000-0000-0000-000000000214'),
    ('00000000-0000-0000-0000-000000000329', 'Máy đóng gói 04', '00000000-0000-0000-0000-000000000215'),
    ('00000000-0000-0000-0000-000000000330', 'Máy in nhãn 02', '00000000-0000-0000-0000-000000000215')
ON CONFLICT ("Id") DO NOTHING;

-- Shift Schedules ("LineId" NULL = site-wide shift, applies to every Line at that Site)
INSERT INTO "ShiftSchedule" ("Id", "SiteId", "LineId", "Name", "StartTime", "EndTime") VALUES
    ('00000000-0000-0000-0000-000000000401', '00000000-0000-0000-0000-000000000101', NULL, 'Ca Sáng', '06:00', '14:00'),
    ('00000000-0000-0000-0000-000000000402', '00000000-0000-0000-0000-000000000101', NULL, 'Ca Chiều', '14:00', '22:00'),
    ('00000000-0000-0000-0000-000000000403', '00000000-0000-0000-0000-000000000101', NULL, 'Ca Đêm', '22:00', '06:00'),
    ('00000000-0000-0000-0000-000000000404', '00000000-0000-0000-0000-000000000101', '00000000-0000-0000-0000-000000000201', 'Ca Đêm - Line 1', '22:00', '06:00'),
    ('00000000-0000-0000-0000-000000000405', '00000000-0000-0000-0000-000000000102', NULL, 'Ca Sáng', '06:00', '14:00'),
    ('00000000-0000-0000-0000-000000000406', '00000000-0000-0000-0000-000000000102', NULL, 'Ca Chiều', '14:00', '22:00'),
    ('00000000-0000-0000-0000-000000000407', '00000000-0000-0000-0000-000000000102', NULL, 'Ca Đêm', '22:00', '06:00'),
    ('00000000-0000-0000-0000-000000000408', '00000000-0000-0000-0000-000000000103', NULL, 'Ca Sáng', '06:00', '14:00'),
    ('00000000-0000-0000-0000-000000000409', '00000000-0000-0000-0000-000000000103', NULL, 'Ca Chiều', '14:00', '22:00'),
    ('00000000-0000-0000-0000-000000000410', '00000000-0000-0000-0000-000000000103', NULL, 'Ca Đêm', '22:00', '06:00'),
    ('00000000-0000-0000-0000-000000000411', '00000000-0000-0000-0000-000000000104', NULL, 'Ca Sáng', '06:00', '14:00'),
    ('00000000-0000-0000-0000-000000000412', '00000000-0000-0000-0000-000000000104', NULL, 'Ca Chiều', '14:00', '22:00'),
    ('00000000-0000-0000-0000-000000000413', '00000000-0000-0000-0000-000000000104', NULL, 'Ca Đêm', '22:00', '06:00'),
    ('00000000-0000-0000-0000-000000000414', '00000000-0000-0000-0000-000000000105', NULL, 'Ca Sáng', '06:00', '14:00'),
    ('00000000-0000-0000-0000-000000000415', '00000000-0000-0000-0000-000000000105', NULL, 'Ca Chiều', '14:00', '22:00'),
    ('00000000-0000-0000-0000-000000000416', '00000000-0000-0000-0000-000000000105', NULL, 'Ca Đêm', '22:00', '06:00')
ON CONFLICT ("Id") DO NOTHING;

-- Reason Codes ("LossCategory": 0 = AvailabilityLoss, 1 = PerformanceLoss, 2 = QualityLoss — AD-5).
-- Each Site draws 7 distinct reason types from a shared 12-type library, covering all 3
-- categories, so the Loss Pie Chart is both meaningful and varied from any scoped demo User's view.
INSERT INTO "ReasonCode" ("Id", "SiteId", "Name", "LossCategory", "IsActive") VALUES
    ('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000101', 'Hỏng máy / Breakdown', 0, true),
    ('00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-000000000101', 'Chờ nguyên liệu / Waiting Material', 0, true),
    ('00000000-0000-0000-0000-000000000503', '00000000-0000-0000-0000-000000000101', 'Bảo trì định kỳ / Preventive Maintenance', 0, true),
    ('00000000-0000-0000-0000-000000000504', '00000000-0000-0000-0000-000000000101', 'Đổi khuôn / Changeover', 1, true),
    ('00000000-0000-0000-0000-000000000505', '00000000-0000-0000-0000-000000000101', 'Kẹt máy / Machine Jam', 1, true),
    ('00000000-0000-0000-0000-000000000506', '00000000-0000-0000-0000-000000000101', 'Lỗi chất lượng / Quality Defect', 2, true),
    ('00000000-0000-0000-0000-000000000507', '00000000-0000-0000-0000-000000000101', 'Nguyên liệu lỗi / Defective Material', 2, true),
    ('00000000-0000-0000-0000-000000000508', '00000000-0000-0000-0000-000000000102', 'Hỏng máy / Breakdown', 0, true),
    ('00000000-0000-0000-0000-000000000509', '00000000-0000-0000-0000-000000000102', 'Mất điện / Power Outage', 0, true),
    ('00000000-0000-0000-0000-000000000510', '00000000-0000-0000-0000-000000000102', 'Thiếu nhân lực / Understaffed', 0, true),
    ('00000000-0000-0000-0000-000000000511', '00000000-0000-0000-0000-000000000102', 'Đổi khuôn / Changeover', 1, true),
    ('00000000-0000-0000-0000-000000000512', '00000000-0000-0000-0000-000000000102', 'Tốc độ giảm / Reduced Speed', 1, true),
    ('00000000-0000-0000-0000-000000000513', '00000000-0000-0000-0000-000000000102', 'Lỗi chất lượng / Quality Defect', 2, true),
    ('00000000-0000-0000-0000-000000000514', '00000000-0000-0000-0000-000000000102', 'Chờ kiểm tra QC / Waiting QC Inspection', 2, true),
    ('00000000-0000-0000-0000-000000000515', '00000000-0000-0000-0000-000000000103', 'Hỏng máy / Breakdown', 0, true),
    ('00000000-0000-0000-0000-000000000516', '00000000-0000-0000-0000-000000000103', 'Chờ nguyên liệu / Waiting Material', 0, true),
    ('00000000-0000-0000-0000-000000000517', '00000000-0000-0000-0000-000000000103', 'Bảo trì định kỳ / Preventive Maintenance', 0, true),
    ('00000000-0000-0000-0000-000000000518', '00000000-0000-0000-0000-000000000103', 'Kẹt máy / Machine Jam', 1, true),
    ('00000000-0000-0000-0000-000000000519', '00000000-0000-0000-0000-000000000103', 'Hiệu chỉnh máy / Calibration', 1, true),
    ('00000000-0000-0000-0000-000000000520', '00000000-0000-0000-0000-000000000103', 'Lỗi chất lượng / Quality Defect', 2, true),
    ('00000000-0000-0000-0000-000000000521', '00000000-0000-0000-0000-000000000103', 'Nguyên liệu lỗi / Defective Material', 2, true),
    ('00000000-0000-0000-0000-000000000522', '00000000-0000-0000-0000-000000000104', 'Hỏng máy / Breakdown', 0, true),
    ('00000000-0000-0000-0000-000000000523', '00000000-0000-0000-0000-000000000104', 'Mất điện / Power Outage', 0, true),
    ('00000000-0000-0000-0000-000000000524', '00000000-0000-0000-0000-000000000104', 'Bảo trì định kỳ / Preventive Maintenance', 0, true),
    ('00000000-0000-0000-0000-000000000525', '00000000-0000-0000-0000-000000000104', 'Đổi khuôn / Changeover', 1, true),
    ('00000000-0000-0000-0000-000000000526', '00000000-0000-0000-0000-000000000104', 'Hiệu chỉnh máy / Calibration', 1, true),
    ('00000000-0000-0000-0000-000000000527', '00000000-0000-0000-0000-000000000104', 'Nguyên liệu lỗi / Defective Material', 2, true),
    ('00000000-0000-0000-0000-000000000528', '00000000-0000-0000-0000-000000000104', 'Chờ kiểm tra QC / Waiting QC Inspection', 2, true),
    ('00000000-0000-0000-0000-000000000529', '00000000-0000-0000-0000-000000000105', 'Chờ nguyên liệu / Waiting Material', 0, true),
    ('00000000-0000-0000-0000-000000000530', '00000000-0000-0000-0000-000000000105', 'Thiếu nhân lực / Understaffed', 0, true),
    ('00000000-0000-0000-0000-000000000531', '00000000-0000-0000-0000-000000000105', 'Bảo trì định kỳ / Preventive Maintenance', 0, true),
    ('00000000-0000-0000-0000-000000000532', '00000000-0000-0000-0000-000000000105', 'Tốc độ giảm / Reduced Speed', 1, true),
    ('00000000-0000-0000-0000-000000000533', '00000000-0000-0000-0000-000000000105', 'Kẹt máy / Machine Jam', 1, true),
    ('00000000-0000-0000-0000-000000000534', '00000000-0000-0000-0000-000000000105', 'Lỗi chất lượng / Quality Defect', 2, true),
    ('00000000-0000-0000-0000-000000000535', '00000000-0000-0000-0000-000000000105', 'Chờ kiểm tra QC / Waiting QC Inspection', 2, true)
ON CONFLICT ("Id") DO NOTHING;

-- Users (password hashes are ASP.NET Core Identity PasswordHasher<T> output for "admin").
-- One Manager + one Viewer per Site, plus two Operators per Site (one per Line) so every Site
-- has a full scoped-role demo set and Operators map onto real Lines.
INSERT INTO "User" ("Id", "Username", "Role", "PasswordHash", "SiteIds", "LineIds") VALUES
    ('00000000-0000-0000-0000-000000000601', 'manager1', 'Manager',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000101']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000602', 'operator1', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000101']::uuid[], ARRAY['00000000-0000-0000-0000-000000000201']::uuid[]),
    ('00000000-0000-0000-0000-000000000603', 'operator2', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000101']::uuid[], ARRAY['00000000-0000-0000-0000-000000000202']::uuid[]),
    ('00000000-0000-0000-0000-000000000604', 'viewer1', 'Viewer',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000101']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000605', 'manager2', 'Manager',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000102']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000606', 'operator3', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000102']::uuid[], ARRAY['00000000-0000-0000-0000-000000000204']::uuid[]),
    ('00000000-0000-0000-0000-000000000607', 'operator4', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000102']::uuid[], ARRAY['00000000-0000-0000-0000-000000000205']::uuid[]),
    ('00000000-0000-0000-0000-000000000608', 'viewer2', 'Viewer',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000102']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000609', 'manager3', 'Manager',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000103']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000610', 'operator5', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000103']::uuid[], ARRAY['00000000-0000-0000-0000-000000000207']::uuid[]),
    ('00000000-0000-0000-0000-000000000611', 'operator6', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000103']::uuid[], ARRAY['00000000-0000-0000-0000-000000000208']::uuid[]),
    ('00000000-0000-0000-0000-000000000612', 'viewer3', 'Viewer',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000103']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000613', 'manager4', 'Manager',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000104']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000614', 'operator7', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000104']::uuid[], ARRAY['00000000-0000-0000-0000-000000000210']::uuid[]),
    ('00000000-0000-0000-0000-000000000615', 'operator8', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000104']::uuid[], ARRAY['00000000-0000-0000-0000-000000000211']::uuid[]),
    ('00000000-0000-0000-0000-000000000616', 'viewer4', 'Viewer',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000104']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000617', 'manager5', 'Manager',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000105']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000618', 'operator9', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000105']::uuid[], ARRAY['00000000-0000-0000-0000-000000000213']::uuid[]),
    ('00000000-0000-0000-0000-000000000619', 'operator10', 'Operator',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000105']::uuid[], ARRAY['00000000-0000-0000-0000-000000000214']::uuid[]),
    ('00000000-0000-0000-0000-000000000620', 'viewer5', 'Viewer',
     'AQAAAAIAAYagAAAAENy+4fORFO9SQXw50VkFkVqks+NZpF8HroV2BqvDx7U6cBDz4glDaHIyphdmrHiotg==',
     ARRAY['00000000-0000-0000-0000-000000000105']::uuid[], ARRAY[]::uuid[])
ON CONFLICT ("Id") DO NOTHING;

-- Machine State (current reading per Machine — drives the Dashboard).
-- "Status": 0 = Running, 1 = Stopped, 2 = Idle, 3 = Fault (MachineStatus enum order).
-- Machine 312 ("Máy sấy 02")'s LastReportedAt is intentionally older than the no-signal
-- threshold (default 60s, see ProductionOptions) to demo that state on the Dashboard; every other
-- row is well inside that threshold so it reliably renders as its real status (a Stopped card that
-- reads as no-signal isn't tappable — the no-signal presentation overrides status, see
-- machine-status-card.ts's isNoSignal()).
-- Exactly one machine per Site is Stopped, each paired with that Site's own open DowntimeEvent
-- below — the DB's unique partial index only forbids two open events on the *same* Machine.
-- Unlike the master data above, this uses DO UPDATE, not DO NOTHING: re-running this script refreshes
-- every "LastReportedAt" back to "now", so the Dashboard never looks stale no matter how long ago the
-- script was first run — you just run it again before a demo.
INSERT INTO "MachineState" ("MachineId", "Status", "Counter", "LastReportedAt") VALUES
    ('00000000-0000-0000-0000-000000000301', 1, 7475, now() - interval '38 seconds'),
    ('00000000-0000-0000-0000-000000000302', 0, 10102, now() - interval '8 seconds'),
    ('00000000-0000-0000-0000-000000000303', 3, 12067, now() - interval '12 seconds'),
    ('00000000-0000-0000-0000-000000000304', 2, 14445, now() - interval '5 seconds'),
    ('00000000-0000-0000-0000-000000000305', 0, 14914, now() - interval '16 seconds'),
    ('00000000-0000-0000-0000-000000000306', 0, 17341, now() - interval '16 seconds'),
    ('00000000-0000-0000-0000-000000000307', 1, 18393, now() - interval '21 seconds'),
    ('00000000-0000-0000-0000-000000000308', 0, 19402, now() - interval '11 seconds'),
    ('00000000-0000-0000-0000-000000000309', 3, 22435, now() - interval '6 seconds'),
    ('00000000-0000-0000-0000-000000000310', 2, 22970, now() - interval '12 seconds'),
    ('00000000-0000-0000-0000-000000000311', 0, 25159, now() - interval '5 seconds'),
    ('00000000-0000-0000-0000-000000000312', 0, 28999, now() - interval '5 minutes 30 seconds'),
    ('00000000-0000-0000-0000-000000000313', 1, 33078, now() - interval '17 seconds'),
    ('00000000-0000-0000-0000-000000000314', 0, 37019, now() - interval '17 seconds'),
    ('00000000-0000-0000-0000-000000000315', 3, 37496, now() - interval '18 seconds'),
    ('00000000-0000-0000-0000-000000000316', 2, 40389, now() - interval '14 seconds'),
    ('00000000-0000-0000-0000-000000000317', 0, 44351, now() - interval '17 seconds'),
    ('00000000-0000-0000-0000-000000000318', 0, 45328, now() - interval '12 seconds'),
    ('00000000-0000-0000-0000-000000000319', 1, 49140, now() - interval '5 seconds'),
    ('00000000-0000-0000-0000-000000000320', 0, 50188, now() - interval '14 seconds'),
    ('00000000-0000-0000-0000-000000000321', 3, 53709, now() - interval '18 seconds'),
    ('00000000-0000-0000-0000-000000000322', 2, 57102, now() - interval '9 seconds'),
    ('00000000-0000-0000-0000-000000000323', 0, 60447, now() - interval '15 seconds'),
    ('00000000-0000-0000-0000-000000000324', 0, 63813, now() - interval '7 seconds'),
    ('00000000-0000-0000-0000-000000000325', 1, 65695, now() - interval '34 seconds'),
    ('00000000-0000-0000-0000-000000000326', 0, 69060, now() - interval '11 seconds'),
    ('00000000-0000-0000-0000-000000000327', 3, 71349, now() - interval '6 seconds'),
    ('00000000-0000-0000-0000-000000000328', 2, 72444, now() - interval '18 seconds'),
    ('00000000-0000-0000-0000-000000000329', 0, 74346, now() - interval '19 seconds'),
    ('00000000-0000-0000-0000-000000000330', 0, 76342, now() - interval '18 seconds')
ON CONFLICT ("MachineId") DO UPDATE SET
    "Status" = EXCLUDED."Status",
    "Counter" = EXCLUDED."Counter",
    "LastReportedAt" = EXCLUDED."LastReportedAt";

-- Downtime Events. Reason codes (when set) always come from the same Site as the Machine, matching
-- what the app itself enforces. This first block is one currently-open event per Site (EndedAt NULL,
-- pairs with that Site's Stopped Machine above).
INSERT INTO "DowntimeEvent" ("Id", "MachineId", "ReasonCodeId", "StartedAt", "EndedAt") VALUES
    ('00000000-0000-0000-0000-000000000701', '00000000-0000-0000-0000-000000000301', NULL, now() - interval '28 seconds', NULL),
    ('00000000-0000-0000-0000-000000000702', '00000000-0000-0000-0000-000000000307', '00000000-0000-0000-0000-000000000513', now() - interval '40 seconds', NULL),
    ('00000000-0000-0000-0000-000000000703', '00000000-0000-0000-0000-000000000313', NULL, now() - interval '10 seconds', NULL),
    ('00000000-0000-0000-0000-000000000704', '00000000-0000-0000-0000-000000000319', '00000000-0000-0000-0000-000000000524', now() - interval '40 seconds', NULL),
    ('00000000-0000-0000-0000-000000000705', '00000000-0000-0000-0000-000000000325', NULL, now() - interval '43 seconds', NULL)
ON CONFLICT ("Id") DO NOTHING;

-- Closed downtime history — generated rather than hand-listed, because the report math needs volume
-- to mean anything. OEE here reduces to `1 - totalLoss / plannedTime` (see OeeReportQueryUseCase),
-- and plannedTime is the FULL calendar day per machine — there is no operating-calendar entity to
-- subtract nights or weekends. A hand-written handful of 40-minute stoppages is therefore <1% of
-- 30 machines x 24h, which lands OEE at 99%+ and draws a dead-flat trend line. Three events per
-- machine-day averaging 5.6h in total put OEE around 77% with visible day-to-day movement.
--
-- Ids are md5-derived from (machine, day, category) so re-running stays idempotent under
-- ON CONFLICT, exactly like the fixed-id rows above. Each machine-day gets one event per
-- LossCategory in a non-overlapping time window — two overlapping closed events on one machine
-- would double-count that machine's loss — with base durations weighted 3.0h / 1.7h / 0.9h so the
-- Loss Breakdown doughnut has a realistic Availability-dominant shape.
--
-- `('x' || substr(md5(...), 1, 7))::bit(28)::int` is the deterministic-pseudo-random idiom: 28 bits
-- keeps it inside a non-negative int, so `% n` never yields a negative offset.
INSERT INTO "DowntimeEvent" ("Id", "MachineId", "ReasonCodeId", "StartedAt", "EndedAt")
SELECT
    md5('downtime|' || m."Id"::text || '|' || d::text || '|' || slot.category::text)::uuid,
    m."Id",
    rc."Id",
    slot.started_at,
    -- Clamped so "today" never contains a closed event that ends in the future.
    least(slot.started_at + make_interval(mins => slot.duration_minutes), now() - interval '2 minutes')
FROM "Machine" m
JOIN "Line" l ON l."Id" = m."LineId"
CROSS JOIN generate_series(0, 29) AS d
-- (LossCategory, window start minute-of-day, base duration minutes, window span minutes)
CROSS JOIN (VALUES (0, 0, 180, 180), (1, 540, 102, 120), (2, 900, 54, 120))
    AS cat(category, window_start_minute, base_duration_minutes, window_span_minutes)
CROSS JOIN LATERAL (
    SELECT
        cat.category,
        date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'
            - make_interval(days => d)
            + make_interval(mins => cat.window_start_minute
                + (('x' || substr(md5('start|' || m."Id"::text || d::text || cat.category::text), 1, 7))::bit(28)::int
                   % cat.window_span_minutes)) AS started_at,
        -- 0.60x..1.40x per machine, times a 0.70x..1.30x factor shared by every machine on that day,
        -- so the trend line moves as one plant rather than as 30 independent random walks.
        greatest(10, (cat.base_duration_minutes
            * (60 + (('x' || substr(md5('dur|' || m."Id"::text || d::text || cat.category::text), 1, 7))::bit(28)::int % 81))
            * (70 + (('x' || substr(md5('day|' || d::text), 1, 7))::bit(28)::int % 61))
            / 10000)::int) AS duration_minutes
) AS slot
-- A Site missing an active ReasonCode for this category simply yields NULL here, which is a valid
-- unattributed stoppage (it shows in the report's "unattributed" figure), not a broken row.
LEFT JOIN LATERAL (
    SELECT r."Id"
    FROM "ReasonCode" r
    WHERE r."SiteId" = l."SiteId" AND r."LossCategory" = slot.category AND r."IsActive"
    ORDER BY md5('reason|' || r."Id"::text || m."Id"::text || d::text)
    LIMIT 1
) rc ON true
WHERE slot.started_at < now() - interval '10 minutes'
ON CONFLICT ("Id") DO NOTHING;

-- Quality Rejects — one per machine per day over the same 30-day window, so the Dashboard's
-- "Quality rejects" tile and the Loss Breakdown caption both have something to show on any date.
INSERT INTO "QualityReject" ("Id", "MachineId", "Quantity", "RecordedAt")
SELECT
    md5('reject|' || m."Id"::text || '|' || d::text)::uuid,
    m."Id",
    1 + (('x' || substr(md5('qty|' || m."Id"::text || d::text), 1, 7))::bit(28)::int % 14),
    r.recorded_at
FROM "Machine" m
CROSS JOIN generate_series(0, 29) AS d
CROSS JOIN LATERAL (
    SELECT date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'
        - make_interval(days => d)
        + make_interval(mins => 360
            + (('x' || substr(md5('at|' || m."Id"::text || d::text), 1, 7))::bit(28)::int % 720)) AS recorded_at
) AS r
WHERE r.recorded_at < now()
ON CONFLICT ("Id") DO NOTHING;

COMMIT;
