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
-- what the app itself enforces. The first block is one currently-open event per Site (EndedAt NULL,
-- pairs with that Site's Stopped Machine above); the rest is closed history spread over the last
-- ~5 days across every Machine.
INSERT INTO "DowntimeEvent" ("Id", "MachineId", "ReasonCodeId", "StartedAt", "EndedAt") VALUES
    ('00000000-0000-0000-0000-000000000701', '00000000-0000-0000-0000-000000000301', NULL, now() - interval '28 seconds', NULL),
    ('00000000-0000-0000-0000-000000000702', '00000000-0000-0000-0000-000000000307', '00000000-0000-0000-0000-000000000513', now() - interval '40 seconds', NULL),
    ('00000000-0000-0000-0000-000000000703', '00000000-0000-0000-0000-000000000313', NULL, now() - interval '10 seconds', NULL),
    ('00000000-0000-0000-0000-000000000704', '00000000-0000-0000-0000-000000000319', '00000000-0000-0000-0000-000000000524', now() - interval '40 seconds', NULL),
    ('00000000-0000-0000-0000-000000000705', '00000000-0000-0000-0000-000000000325', NULL, now() - interval '43 seconds', NULL),
    ('00000000-0000-0000-0000-000000000706', '00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000506', now() - interval '3 days 21 hours 19 minutes', now() - interval '3 days 20 hours 28 minutes'),
    ('00000000-0000-0000-0000-000000000707', '00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000501', now() - interval '3 days 22 hours 14 minutes', now() - interval '3 days 21 hours 1 minutes'),
    ('00000000-0000-0000-0000-000000000708', '00000000-0000-0000-0000-000000000302', '00000000-0000-0000-0000-000000000505', now() - interval '4 days 17 hours 40 minutes', now() - interval '4 days 16 hours 29 minutes'),
    ('00000000-0000-0000-0000-000000000709', '00000000-0000-0000-0000-000000000303', NULL, now() - interval '4 days 6 hours 16 minutes', now() - interval '4 days 5 hours 19 minutes'),
    ('00000000-0000-0000-0000-000000000710', '00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-000000000507', now() - interval '3 days 8 hours 49 minutes', now() - interval '3 days 8 hours 2 minutes'),
    ('00000000-0000-0000-0000-000000000711', '00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-000000000502', now() - interval '0 days 1 hours 43 minutes', now() - interval '0 days 1 hours 10 minutes'),
    ('00000000-0000-0000-0000-000000000712', '00000000-0000-0000-0000-000000000305', '00000000-0000-0000-0000-000000000505', now() - interval '3 days 2 hours 9 minutes', now() - interval '3 days 1 hours 10 minutes'),
    ('00000000-0000-0000-0000-000000000713', '00000000-0000-0000-0000-000000000306', '00000000-0000-0000-0000-000000000505', now() - interval '1 days 14 hours 47 minutes', now() - interval '1 days 13 hours 56 minutes'),
    ('00000000-0000-0000-0000-000000000714', '00000000-0000-0000-0000-000000000307', '00000000-0000-0000-0000-000000000510', now() - interval '2 days 3 hours 35 minutes', now() - interval '2 days 3 hours 19 minutes'),
    ('00000000-0000-0000-0000-000000000715', '00000000-0000-0000-0000-000000000307', '00000000-0000-0000-0000-000000000513', now() - interval '0 days 1 hours 28 minutes', now() - interval '0 days 0 hours 29 minutes'),
    ('00000000-0000-0000-0000-000000000716', '00000000-0000-0000-0000-000000000308', '00000000-0000-0000-0000-000000000514', now() - interval '4 days 3 hours 58 minutes', now() - interval '4 days 3 hours 1 minutes'),
    ('00000000-0000-0000-0000-000000000717', '00000000-0000-0000-0000-000000000309', '00000000-0000-0000-0000-000000000508', now() - interval '3 days 22 hours 47 minutes', now() - interval '3 days 21 hours 45 minutes'),
    ('00000000-0000-0000-0000-000000000718', '00000000-0000-0000-0000-000000000310', '00000000-0000-0000-0000-000000000511', now() - interval '2 days 8 hours 56 minutes', now() - interval '2 days 8 hours 27 minutes'),
    ('00000000-0000-0000-0000-000000000719', '00000000-0000-0000-0000-000000000311', '00000000-0000-0000-0000-000000000510', now() - interval '1 days 18 hours 44 minutes', now() - interval '1 days 18 hours 15 minutes'),
    ('00000000-0000-0000-0000-000000000720', '00000000-0000-0000-0000-000000000312', '00000000-0000-0000-0000-000000000508', now() - interval '3 days 22 hours 40 minutes', now() - interval '3 days 22 hours 11 minutes'),
    ('00000000-0000-0000-0000-000000000721', '00000000-0000-0000-0000-000000000313', '00000000-0000-0000-0000-000000000519', now() - interval '2 days 21 hours 47 minutes', now() - interval '2 days 20 hours 54 minutes'),
    ('00000000-0000-0000-0000-000000000722', '00000000-0000-0000-0000-000000000313', NULL, now() - interval '0 days 14 hours 54 minutes', now() - interval '0 days 14 hours 27 minutes'),
    ('00000000-0000-0000-0000-000000000723', '00000000-0000-0000-0000-000000000314', '00000000-0000-0000-0000-000000000515', now() - interval '4 days 19 hours 26 minutes', now() - interval '4 days 18 hours 42 minutes'),
    ('00000000-0000-0000-0000-000000000724', '00000000-0000-0000-0000-000000000314', NULL, now() - interval '0 days 16 hours 10 minutes', now() - interval '0 days 15 hours 4 minutes'),
    ('00000000-0000-0000-0000-000000000725', '00000000-0000-0000-0000-000000000315', '00000000-0000-0000-0000-000000000520', now() - interval '0 days 11 hours 58 minutes', now() - interval '0 days 10 hours 52 minutes'),
    ('00000000-0000-0000-0000-000000000726', '00000000-0000-0000-0000-000000000315', '00000000-0000-0000-0000-000000000520', now() - interval '0 days 1 hours 42 minutes', now() - interval '0 days 0 hours 55 minutes'),
    ('00000000-0000-0000-0000-000000000727', '00000000-0000-0000-0000-000000000316', '00000000-0000-0000-0000-000000000515', now() - interval '2 days 7 hours 32 minutes', now() - interval '2 days 6 hours 48 minutes'),
    ('00000000-0000-0000-0000-000000000728', '00000000-0000-0000-0000-000000000316', '00000000-0000-0000-0000-000000000517', now() - interval '2 days 19 hours 10 minutes', now() - interval '2 days 18 hours 6 minutes'),
    ('00000000-0000-0000-0000-000000000729', '00000000-0000-0000-0000-000000000317', '00000000-0000-0000-0000-000000000516', now() - interval '1 days 13 hours 50 minutes', now() - interval '1 days 12 hours 51 minutes'),
    ('00000000-0000-0000-0000-000000000730', '00000000-0000-0000-0000-000000000318', NULL, now() - interval '4 days 15 hours 35 minutes', now() - interval '4 days 14 hours 49 minutes'),
    ('00000000-0000-0000-0000-000000000731', '00000000-0000-0000-0000-000000000319', '00000000-0000-0000-0000-000000000522', now() - interval '2 days 23 hours 39 minutes', now() - interval '2 days 22 hours 30 minutes'),
    ('00000000-0000-0000-0000-000000000732', '00000000-0000-0000-0000-000000000319', '00000000-0000-0000-0000-000000000524', now() - interval '0 days 19 hours 52 minutes', now() - interval '0 days 19 hours 13 minutes'),
    ('00000000-0000-0000-0000-000000000733', '00000000-0000-0000-0000-000000000320', NULL, now() - interval '1 days 18 hours 42 minutes', now() - interval '1 days 17 hours 35 minutes'),
    ('00000000-0000-0000-0000-000000000734', '00000000-0000-0000-0000-000000000321', '00000000-0000-0000-0000-000000000526', now() - interval '2 days 18 hours 49 minutes', now() - interval '2 days 18 hours 23 minutes'),
    ('00000000-0000-0000-0000-000000000735', '00000000-0000-0000-0000-000000000321', '00000000-0000-0000-0000-000000000525', now() - interval '1 days 22 hours 6 minutes', now() - interval '1 days 21 hours 43 minutes'),
    ('00000000-0000-0000-0000-000000000736', '00000000-0000-0000-0000-000000000322', '00000000-0000-0000-0000-000000000522', now() - interval '1 days 19 hours 43 minutes', now() - interval '1 days 18 hours 53 minutes'),
    ('00000000-0000-0000-0000-000000000737', '00000000-0000-0000-0000-000000000323', '00000000-0000-0000-0000-000000000523', now() - interval '0 days 13 hours 22 minutes', now() - interval '0 days 13 hours 1 minutes'),
    ('00000000-0000-0000-0000-000000000738', '00000000-0000-0000-0000-000000000324', '00000000-0000-0000-0000-000000000527', now() - interval '4 days 4 hours 36 minutes', now() - interval '4 days 4 hours 15 minutes'),
    ('00000000-0000-0000-0000-000000000739', '00000000-0000-0000-0000-000000000324', '00000000-0000-0000-0000-000000000522', now() - interval '4 days 18 hours 16 minutes', now() - interval '4 days 17 hours 49 minutes'),
    ('00000000-0000-0000-0000-000000000740', '00000000-0000-0000-0000-000000000325', '00000000-0000-0000-0000-000000000535', now() - interval '4 days 21 hours 6 minutes', now() - interval '4 days 20 hours 36 minutes'),
    ('00000000-0000-0000-0000-000000000741', '00000000-0000-0000-0000-000000000326', '00000000-0000-0000-0000-000000000535', now() - interval '3 days 4 hours 43 minutes', now() - interval '3 days 4 hours 14 minutes'),
    ('00000000-0000-0000-0000-000000000742', '00000000-0000-0000-0000-000000000326', '00000000-0000-0000-0000-000000000535', now() - interval '2 days 18 hours 32 minutes', now() - interval '2 days 18 hours 15 minutes'),
    ('00000000-0000-0000-0000-000000000743', '00000000-0000-0000-0000-000000000327', NULL, now() - interval '2 days 21 hours 20 minutes', now() - interval '2 days 20 hours 22 minutes'),
    ('00000000-0000-0000-0000-000000000744', '00000000-0000-0000-0000-000000000327', '00000000-0000-0000-0000-000000000531', now() - interval '0 days 15 hours 59 minutes', now() - interval '0 days 15 hours 34 minutes'),
    ('00000000-0000-0000-0000-000000000745', '00000000-0000-0000-0000-000000000328', '00000000-0000-0000-0000-000000000531', now() - interval '1 days 4 hours 12 minutes', now() - interval '1 days 3 hours 24 minutes'),
    ('00000000-0000-0000-0000-000000000746', '00000000-0000-0000-0000-000000000329', '00000000-0000-0000-0000-000000000533', now() - interval '2 days 9 hours 13 minutes', now() - interval '2 days 8 hours 39 minutes'),
    ('00000000-0000-0000-0000-000000000747', '00000000-0000-0000-0000-000000000330', '00000000-0000-0000-0000-000000000534', now() - interval '0 days 1 hours 14 minutes', now() - interval '0 days 0 hours 48 minutes')
ON CONFLICT ("Id") DO NOTHING;

-- Quality Rejects, scattered across every Machine over the last few days.
INSERT INTO "QualityReject" ("Id", "MachineId", "Quantity", "RecordedAt") VALUES
    ('00000000-0000-0000-0000-000000000801', '00000000-0000-0000-0000-000000000301', 12, now() - interval '2 days 20 hours'),
    ('00000000-0000-0000-0000-000000000802', '00000000-0000-0000-0000-000000000301', 2, now() - interval '4 days 22 hours'),
    ('00000000-0000-0000-0000-000000000803', '00000000-0000-0000-0000-000000000302', 9, now() - interval '4 days 3 hours'),
    ('00000000-0000-0000-0000-000000000804', '00000000-0000-0000-0000-000000000303', 8, now() - interval '3 days 23 hours'),
    ('00000000-0000-0000-0000-000000000805', '00000000-0000-0000-0000-000000000303', 11, now() - interval '3 days 5 hours'),
    ('00000000-0000-0000-0000-000000000806', '00000000-0000-0000-0000-000000000304', 12, now() - interval '1 days 15 hours'),
    ('00000000-0000-0000-0000-000000000807', '00000000-0000-0000-0000-000000000304', 11, now() - interval '0 days 7 hours'),
    ('00000000-0000-0000-0000-000000000808', '00000000-0000-0000-0000-000000000305', 4, now() - interval '3 days 13 hours'),
    ('00000000-0000-0000-0000-000000000809', '00000000-0000-0000-0000-000000000305', 11, now() - interval '0 days 8 hours'),
    ('00000000-0000-0000-0000-000000000810', '00000000-0000-0000-0000-000000000306', 10, now() - interval '3 days 7 hours'),
    ('00000000-0000-0000-0000-000000000811', '00000000-0000-0000-0000-000000000307', 1, now() - interval '4 days 13 hours'),
    ('00000000-0000-0000-0000-000000000812', '00000000-0000-0000-0000-000000000308', 3, now() - interval '4 days 23 hours'),
    ('00000000-0000-0000-0000-000000000813', '00000000-0000-0000-0000-000000000309', 8, now() - interval '1 days 8 hours'),
    ('00000000-0000-0000-0000-000000000814', '00000000-0000-0000-0000-000000000310', 6, now() - interval '1 days 6 hours'),
    ('00000000-0000-0000-0000-000000000815', '00000000-0000-0000-0000-000000000311', 9, now() - interval '1 days 21 hours'),
    ('00000000-0000-0000-0000-000000000816', '00000000-0000-0000-0000-000000000311', 7, now() - interval '3 days 1 hours'),
    ('00000000-0000-0000-0000-000000000817', '00000000-0000-0000-0000-000000000312', 4, now() - interval '3 days 9 hours'),
    ('00000000-0000-0000-0000-000000000818', '00000000-0000-0000-0000-000000000313', 6, now() - interval '0 days 13 hours'),
    ('00000000-0000-0000-0000-000000000819', '00000000-0000-0000-0000-000000000314', 8, now() - interval '0 days 4 hours'),
    ('00000000-0000-0000-0000-000000000820', '00000000-0000-0000-0000-000000000315', 2, now() - interval '4 days 8 hours'),
    ('00000000-0000-0000-0000-000000000821', '00000000-0000-0000-0000-000000000316', 10, now() - interval '2 days 3 hours'),
    ('00000000-0000-0000-0000-000000000822', '00000000-0000-0000-0000-000000000317', 1, now() - interval '2 days 18 hours'),
    ('00000000-0000-0000-0000-000000000823', '00000000-0000-0000-0000-000000000318', 10, now() - interval '1 days 15 hours'),
    ('00000000-0000-0000-0000-000000000824', '00000000-0000-0000-0000-000000000319', 10, now() - interval '0 days 10 hours'),
    ('00000000-0000-0000-0000-000000000825', '00000000-0000-0000-0000-000000000320', 9, now() - interval '4 days 1 hours'),
    ('00000000-0000-0000-0000-000000000826', '00000000-0000-0000-0000-000000000321', 8, now() - interval '4 days 12 hours'),
    ('00000000-0000-0000-0000-000000000827', '00000000-0000-0000-0000-000000000322', 11, now() - interval '4 days 7 hours'),
    ('00000000-0000-0000-0000-000000000828', '00000000-0000-0000-0000-000000000323', 9, now() - interval '3 days 23 hours'),
    ('00000000-0000-0000-0000-000000000829', '00000000-0000-0000-0000-000000000323', 6, now() - interval '4 days 4 hours'),
    ('00000000-0000-0000-0000-000000000830', '00000000-0000-0000-0000-000000000324', 4, now() - interval '0 days 19 hours'),
    ('00000000-0000-0000-0000-000000000831', '00000000-0000-0000-0000-000000000325', 2, now() - interval '0 days 3 hours'),
    ('00000000-0000-0000-0000-000000000832', '00000000-0000-0000-0000-000000000325', 7, now() - interval '2 days 21 hours'),
    ('00000000-0000-0000-0000-000000000833', '00000000-0000-0000-0000-000000000326', 1, now() - interval '2 days 12 hours'),
    ('00000000-0000-0000-0000-000000000834', '00000000-0000-0000-0000-000000000327', 7, now() - interval '4 days 4 hours'),
    ('00000000-0000-0000-0000-000000000835', '00000000-0000-0000-0000-000000000327', 5, now() - interval '2 days 16 hours'),
    ('00000000-0000-0000-0000-000000000836', '00000000-0000-0000-0000-000000000328', 2, now() - interval '2 days 0 hours'),
    ('00000000-0000-0000-0000-000000000837', '00000000-0000-0000-0000-000000000329', 10, now() - interval '3 days 15 hours'),
    ('00000000-0000-0000-0000-000000000838', '00000000-0000-0000-0000-000000000330', 1, now() - interval '2 days 20 hours')
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

