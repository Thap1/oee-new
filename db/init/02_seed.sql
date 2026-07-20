-- OeeNew — sample seed data for a fresh environment.
-- Run AFTER 01_schema.sql. Safe to re-run (ON CONFLICT DO NOTHING on fixed ids).
--
-- Sample user passwords (for local/demo use only — rotate before any shared environment):
--   manager1  / Passw0rd!
--   operator1 / Passw0rd!
--   viewer1   / Passw0rd!
--   admin     / ChangeMe123!
-- (Admin login uses the config-driven bootstrap admin — see appsettings "BootstrapAdmin" — not a User row.)

BEGIN;

-- Sites
INSERT INTO "Site" ("Id", "Name") VALUES
    ('00000000-0000-0000-0000-000000000101', 'Nha may Ha Noi'),
    ('00000000-0000-0000-0000-000000000102', 'Nha may Da Nang')
ON CONFLICT ("Id") DO NOTHING;

-- Lines
INSERT INTO "Line" ("Id", "Name", "SiteId") VALUES
    ('00000000-0000-0000-0000-000000000201', 'Line 1', '00000000-0000-0000-0000-000000000101'),
    ('00000000-0000-0000-0000-000000000202', 'Line 2', '00000000-0000-0000-0000-000000000101'),
    ('00000000-0000-0000-0000-000000000203', 'Line 1', '00000000-0000-0000-0000-000000000102')
ON CONFLICT ("Id") DO NOTHING;

-- Machines
INSERT INTO "Machine" ("Id", "Name", "LineId") VALUES
    ('00000000-0000-0000-0000-000000000301', 'May ep nhua 01', '00000000-0000-0000-0000-000000000201'),
    ('00000000-0000-0000-0000-000000000302', 'May ep nhua 02', '00000000-0000-0000-0000-000000000201'),
    ('00000000-0000-0000-0000-000000000303', 'May dong goi 01', '00000000-0000-0000-0000-000000000202'),
    ('00000000-0000-0000-0000-000000000304', 'May det 01', '00000000-0000-0000-0000-000000000203')
ON CONFLICT ("Id") DO NOTHING;

-- Shift Schedules ("LineId" NULL = site-wide shift, applies to every Line at that Site)
INSERT INTO "ShiftSchedule" ("Id", "SiteId", "LineId", "Name", "StartTime", "EndTime") VALUES
    ('00000000-0000-0000-0000-000000000401', '00000000-0000-0000-0000-000000000101', NULL, 'Ca Sang', '06:00', '14:00'),
    ('00000000-0000-0000-0000-000000000402', '00000000-0000-0000-0000-000000000101', NULL, 'Ca Chieu', '14:00', '22:00'),
    ('00000000-0000-0000-0000-000000000403', '00000000-0000-0000-0000-000000000101', '00000000-0000-0000-0000-000000000201', 'Ca Dem - Line 1', '22:00', '06:00'),
    ('00000000-0000-0000-0000-000000000404', '00000000-0000-0000-0000-000000000102', NULL, 'Ca Hanh chinh', '08:00', '17:00')
ON CONFLICT ("Id") DO NOTHING;

-- Reason Codes ("LossCategory": 0 = AvailabilityLoss, 1 = PerformanceLoss, 2 = QualityLoss — AD-5)
INSERT INTO "ReasonCode" ("Id", "SiteId", "Name", "LossCategory", "IsActive") VALUES
    ('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000101', 'Hong may / Breakdown', 0, true),
    ('00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-000000000101', 'Cho nguyen lieu / Waiting Material', 0, true),
    ('00000000-0000-0000-0000-000000000503', '00000000-0000-0000-0000-000000000101', 'Doi khuon / Changeover', 1, true),
    ('00000000-0000-0000-0000-000000000504', '00000000-0000-0000-0000-000000000101', 'Loi chat luong / Quality Defect', 2, true),
    ('00000000-0000-0000-0000-000000000505', '00000000-0000-0000-0000-000000000102', 'Bao tri dinh ky / PM', 0, true)
ON CONFLICT ("Id") DO NOTHING;

-- Users (password hashes are ASP.NET Core Identity PasswordHasher<T> output for "Passw0rd!")
INSERT INTO "User" ("Id", "Username", "Role", "PasswordHash", "SiteIds", "LineIds") VALUES
    ('00000000-0000-0000-0000-000000000601', 'manager1', 'Manager',
     'AQAAAAIAAYagAAAAEJtTPE/YL0lrm4WkpWjr5va/QtrRVZmuoknVKA6McacbSS3hrltXrbXeEpnaLsZR6g==',
     ARRAY['00000000-0000-0000-0000-000000000101']::uuid[], ARRAY[]::uuid[]),
    ('00000000-0000-0000-0000-000000000602', 'operator1', 'Operator',
     'AQAAAAIAAYagAAAAEMFzY7deaKhmM0pQzM2ZmfhvrUNO3Bnjx7lOcFHMcZ7NlRH3BK2CZ24XHQVwr3k2hQ==',
     ARRAY['00000000-0000-0000-0000-000000000101']::uuid[], ARRAY['00000000-0000-0000-0000-000000000201']::uuid[]),
    ('00000000-0000-0000-0000-000000000603', 'viewer1', 'Viewer',
     'AQAAAAIAAYagAAAAEMQWgv2o03+isyhfH8P/3TW8owt2L+7G4ghln3ZAm85ZoHtnldlbp5ldlEFqG58oNw==',
     ARRAY['00000000-0000-0000-0000-000000000102']::uuid[], ARRAY[]::uuid[])
ON CONFLICT ("Id") DO NOTHING;

COMMIT;
