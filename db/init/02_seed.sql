-- OeeNew — sample seed data for a fresh environment.
-- Run AFTER 01_schema.sql (must be the current version — it now includes MachineState/
-- DowntimeEvent/QualityReject; an older 01_schema.sql will make the inserts below fail with
-- "relation does not exist"). Safe to re-run (ON CONFLICT DO NOTHING on fixed ids).
--
-- Sample user passwords (for local/demo use only — rotate before any shared environment):
--   manager1  / Passw0rd!
--   operator1 / Passw0rd!
--   viewer1   / Passw0rd!
--   admin     / ChangeMe123!
-- (Admin login uses the config-driven bootstrap admin — see appsettings "BootstrapAdmin" — not a User row.)
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
    ('00000000-0000-0000-0000-000000000304', 'May det 01', '00000000-0000-0000-0000-000000000203'),
    ('00000000-0000-0000-0000-000000000305', 'May dong goi 02', '00000000-0000-0000-0000-000000000202'),
    ('00000000-0000-0000-0000-000000000306', 'May det 02', '00000000-0000-0000-0000-000000000203')
ON CONFLICT ("Id") DO NOTHING;

-- Shift Schedules ("LineId" NULL = site-wide shift, applies to every Line at that Site)
INSERT INTO "ShiftSchedule" ("Id", "SiteId", "LineId", "Name", "StartTime", "EndTime") VALUES
    ('00000000-0000-0000-0000-000000000401', '00000000-0000-0000-0000-000000000101', NULL, 'Ca Sang', '06:00', '14:00'),
    ('00000000-0000-0000-0000-000000000402', '00000000-0000-0000-0000-000000000101', NULL, 'Ca Chieu', '14:00', '22:00'),
    ('00000000-0000-0000-0000-000000000403', '00000000-0000-0000-0000-000000000101', '00000000-0000-0000-0000-000000000201', 'Ca Dem - Line 1', '22:00', '06:00'),
    ('00000000-0000-0000-0000-000000000404', '00000000-0000-0000-0000-000000000102', NULL, 'Ca Hanh chinh', '08:00', '17:00')
ON CONFLICT ("Id") DO NOTHING;

-- Reason Codes ("LossCategory": 0 = AvailabilityLoss, 1 = PerformanceLoss, 2 = QualityLoss — AD-5).
-- Both Sites carry all 3 categories so the Loss Pie Chart is meaningful from any scoped demo
-- User's default view (viewer1 is scoped to Site 102 only — see Users below).
INSERT INTO "ReasonCode" ("Id", "SiteId", "Name", "LossCategory", "IsActive") VALUES
    ('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000101', 'Hong may / Breakdown', 0, true),
    ('00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-000000000101', 'Cho nguyen lieu / Waiting Material', 0, true),
    ('00000000-0000-0000-0000-000000000503', '00000000-0000-0000-0000-000000000101', 'Doi khuon / Changeover', 1, true),
    ('00000000-0000-0000-0000-000000000504', '00000000-0000-0000-0000-000000000101', 'Loi chat luong / Quality Defect', 2, true),
    ('00000000-0000-0000-0000-000000000505', '00000000-0000-0000-0000-000000000102', 'Bao tri dinh ky / PM', 0, true),
    ('00000000-0000-0000-0000-000000000506', '00000000-0000-0000-0000-000000000102', 'Doi khuon / Changeover', 1, true),
    ('00000000-0000-0000-0000-000000000507', '00000000-0000-0000-0000-000000000102', 'Loi chat luong / Quality Defect', 2, true)
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

-- Machine State (current reading per Machine — drives the Dashboard).
-- "Status": 0 = Running, 1 = Stopped, 2 = Idle, 3 = Fault (MachineStatus enum order).
-- Machine 306's LastReportedAt is intentionally older than the no-signal threshold (default 60s,
-- see ProductionOptions) to demo that state on the Dashboard; every other row is well inside that
-- threshold so it reliably renders as its real status (a Stopped card that reads as no-signal isn't
-- tappable — the no-signal presentation overrides status, see machine-status-card.ts's isNoSignal()).
-- Unlike the master data above, this uses DO UPDATE, not DO NOTHING: re-running this script refreshes
-- every "LastReportedAt" back to "now", so the Dashboard never looks stale no matter how long ago the
-- script was first run — you just run it again before a demo.
INSERT INTO "MachineState" ("MachineId", "Status", "Counter", "LastReportedAt") VALUES
    ('00000000-0000-0000-0000-000000000301', 0, 15234, now() - interval '10 seconds'),
    ('00000000-0000-0000-0000-000000000302', 1, 8820, now() - interval '15 seconds'),
    ('00000000-0000-0000-0000-000000000303', 0, 40211, now() - interval '5 seconds'),
    ('00000000-0000-0000-0000-000000000304', 3, 5010, now() - interval '12 seconds'),
    ('00000000-0000-0000-0000-000000000305', 2, 12890, now() - interval '8 seconds'),
    ('00000000-0000-0000-0000-000000000306', 0, 30044, now() - interval '5 minutes')
ON CONFLICT ("MachineId") DO UPDATE SET
    "Status" = EXCLUDED."Status",
    "Counter" = EXCLUDED."Counter",
    "LastReportedAt" = EXCLUDED."LastReportedAt";

-- Downtime Events. Reason codes are picked from the same Site as the Machine (501-504/506-507 for
-- Site 101/Hanoi and Site 102/Da Nang respectively), matching what the app itself enforces, and
-- deliberately cover all 3 LossCategory values on *both* Sites and on Line 201 specifically — so
-- the Loss Pie Chart isn't missing a slice for manager1/operator1/viewer1's own default scope.
-- Row 709 is the only currently-open event (EndedAt NULL), matching Machine 302's Stopped state
-- above — the DB's unique partial index allows at most one open event per machine, not one each.
INSERT INTO "DowntimeEvent" ("Id", "MachineId", "ReasonCodeId", "StartedAt", "EndedAt") VALUES
    ('00000000-0000-0000-0000-000000000701', '00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000501', now() - interval '4 days 3 hours 45 minutes', now() - interval '4 days 3 hours'),
    ('00000000-0000-0000-0000-000000000702', '00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000503', now() - interval '3 days 6 hours 20 minutes', now() - interval '3 days 6 hours'),
    ('00000000-0000-0000-0000-000000000703', '00000000-0000-0000-0000-000000000303', '00000000-0000-0000-0000-000000000502', now() - interval '3 days 2 hours', now() - interval '3 days 1 hour'),
    ('00000000-0000-0000-0000-000000000704', '00000000-0000-0000-0000-000000000303', '00000000-0000-0000-0000-000000000504', now() - interval '2 days 5 hours 15 minutes', now() - interval '2 days 5 hours'),
    ('00000000-0000-0000-0000-000000000705', '00000000-0000-0000-0000-000000000305', '00000000-0000-0000-0000-000000000501', now() - interval '2 days 2 hours 30 minutes', now() - interval '2 days 2 hours'),
    ('00000000-0000-0000-0000-000000000706', '00000000-0000-0000-0000-000000000305', '00000000-0000-0000-0000-000000000503', now() - interval '1 day 4 hours 25 minutes', now() - interval '1 day 4 hours'),
    ('00000000-0000-0000-0000-000000000707', '00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-000000000505', now() - interval '1 day 1 hour 50 minutes', now() - interval '1 day 1 hour'),
    ('00000000-0000-0000-0000-000000000708', '00000000-0000-0000-0000-000000000306', NULL, now() - interval '10 hours 12 minutes', now() - interval '10 hours'),
    ('00000000-0000-0000-0000-000000000710', '00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000504', now() - interval '6 hours 18 minutes', now() - interval '6 hours'),
    ('00000000-0000-0000-0000-000000000711', '00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-000000000506', now() - interval '2 days 8 hours 22 minutes', now() - interval '2 days 8 hours'),
    ('00000000-0000-0000-0000-000000000712', '00000000-0000-0000-0000-000000000306', '00000000-0000-0000-0000-000000000507', now() - interval '4 hours 14 minutes', now() - interval '4 hours'),
    ('00000000-0000-0000-0000-000000000709', '00000000-0000-0000-0000-000000000302', NULL, now() - interval '15 seconds', NULL)
ON CONFLICT ("Id") DO NOTHING;

-- Quality Rejects, scattered across machines over the last few days.
INSERT INTO "QualityReject" ("Id", "MachineId", "Quantity", "RecordedAt") VALUES
    ('00000000-0000-0000-0000-000000000801', '00000000-0000-0000-0000-000000000301', 3, now() - interval '4 days 2 hours'),
    ('00000000-0000-0000-0000-000000000802', '00000000-0000-0000-0000-000000000301', 7, now() - interval '2 days 5 hours'),
    ('00000000-0000-0000-0000-000000000803', '00000000-0000-0000-0000-000000000303', 12, now() - interval '3 days 8 hours'),
    ('00000000-0000-0000-0000-000000000804', '00000000-0000-0000-0000-000000000303', 2, now() - interval '1 day 3 hours'),
    ('00000000-0000-0000-0000-000000000805', '00000000-0000-0000-0000-000000000304', 5, now() - interval '2 days 1 hour'),
    ('00000000-0000-0000-0000-000000000806', '00000000-0000-0000-0000-000000000305', 9, now() - interval '1 day 6 hours'),
    ('00000000-0000-0000-0000-000000000807', '00000000-0000-0000-0000-000000000306', 4, now() - interval '12 hours'),
    ('00000000-0000-0000-0000-000000000808', '00000000-0000-0000-0000-000000000302', 1, now() - interval '6 hours')
ON CONFLICT ("Id") DO NOTHING;

COMMIT;
