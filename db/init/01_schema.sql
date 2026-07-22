CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719020139_InitialMasterData') THEN
    CREATE TABLE "Site" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "Name" character varying(200) NOT NULL,
        CONSTRAINT "PK_Site" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719020139_InitialMasterData') THEN
    CREATE TABLE "Line" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "Name" character varying(200) NOT NULL,
        "SiteId" uuid NOT NULL,
        CONSTRAINT "PK_Line" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Line_Site_SiteId" FOREIGN KEY ("SiteId") REFERENCES "Site" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719020139_InitialMasterData') THEN
    CREATE TABLE "Machine" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "Name" character varying(200) NOT NULL,
        "LineId" uuid NOT NULL,
        CONSTRAINT "PK_Machine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Machine_Line_LineId" FOREIGN KEY ("LineId") REFERENCES "Line" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719020139_InitialMasterData') THEN
    CREATE INDEX "IX_Line_SiteId" ON "Line" ("SiteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719020139_InitialMasterData') THEN
    CREATE INDEX "IX_Machine_LineId" ON "Machine" ("LineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719020139_InitialMasterData') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260719020139_InitialMasterData', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719025509_AddShiftSchedule') THEN
    CREATE TABLE "ShiftSchedule" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "SiteId" uuid NOT NULL,
        "LineId" uuid,
        "Name" character varying(200) NOT NULL,
        "StartTime" time NOT NULL,
        "EndTime" time NOT NULL,
        CONSTRAINT "PK_ShiftSchedule" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ShiftSchedule_Line_LineId" FOREIGN KEY ("LineId") REFERENCES "Line" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ShiftSchedule_Site_SiteId" FOREIGN KEY ("SiteId") REFERENCES "Site" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719025509_AddShiftSchedule') THEN
    CREATE INDEX "IX_ShiftSchedule_LineId" ON "ShiftSchedule" ("LineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719025509_AddShiftSchedule') THEN
    CREATE INDEX "IX_ShiftSchedule_SiteId" ON "ShiftSchedule" ("SiteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260719025509_AddShiftSchedule') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260719025509_AddShiftSchedule', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720130438_AddUser') THEN
    CREATE TABLE "User" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "Username" character varying(100) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "PasswordHash" text NOT NULL,
        "SiteIds" uuid[] NOT NULL,
        "LineIds" uuid[] NOT NULL,
        CONSTRAINT "PK_User" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720130438_AddUser') THEN
    CREATE UNIQUE INDEX "IX_User_Username" ON "User" ("Username");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720130438_AddUser') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260720130438_AddUser', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720135632_AddReasonCode') THEN
    CREATE TABLE "ReasonCode" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "SiteId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "LossCategory" smallint NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        CONSTRAINT "PK_ReasonCode" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReasonCode_Site_SiteId" FOREIGN KEY ("SiteId") REFERENCES "Site" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720135632_AddReasonCode') THEN
    CREATE INDEX "IX_ReasonCode_SiteId" ON "ReasonCode" ("SiteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260720135632_AddReasonCode') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260720135632_AddReasonCode', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721131824_AddMachineState') THEN
    CREATE TABLE "MachineState" (
        "MachineId" uuid NOT NULL,
        "Status" smallint NOT NULL,
        "Counter" bigint NOT NULL,
        "LastReportedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_MachineState" PRIMARY KEY ("MachineId"),
        CONSTRAINT "FK_MachineState_Machine_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machine" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721131824_AddMachineState') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260721131824_AddMachineState', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721142436_AddDowntimeEvent') THEN
    CREATE TABLE "DowntimeEvent" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "MachineId" uuid NOT NULL,
        "ReasonCodeId" uuid,
        "StartedAt" timestamp with time zone NOT NULL,
        "EndedAt" timestamp with time zone,
        CONSTRAINT "PK_DowntimeEvent" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DowntimeEvent_Machine_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DowntimeEvent_ReasonCode_ReasonCodeId" FOREIGN KEY ("ReasonCodeId") REFERENCES "ReasonCode" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721142436_AddDowntimeEvent') THEN
    CREATE INDEX "IX_DowntimeEvent_MachineId" ON "DowntimeEvent" ("MachineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721142436_AddDowntimeEvent') THEN
    CREATE INDEX "IX_DowntimeEvent_ReasonCodeId" ON "DowntimeEvent" ("ReasonCodeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721142436_AddDowntimeEvent') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260721142436_AddDowntimeEvent', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721145129_AddQualityReject') THEN
    CREATE TABLE "QualityReject" (
        "Id" uuid NOT NULL DEFAULT (uuidv7()),
        "MachineId" uuid NOT NULL,
        "Quantity" integer NOT NULL,
        "RecordedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_QualityReject" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_QualityReject_Machine_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machine" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721145129_AddQualityReject') THEN
    CREATE INDEX "IX_QualityReject_MachineId" ON "QualityReject" ("MachineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260721145129_AddQualityReject') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260721145129_AddQualityReject', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722113021_AddDowntimeEventOpenUniqueIndex') THEN
    DROP INDEX "IX_DowntimeEvent_MachineId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722113021_AddDowntimeEventOpenUniqueIndex') THEN
    CREATE UNIQUE INDEX "IX_DowntimeEvent_MachineId_OpenOnly" ON "DowntimeEvent" ("MachineId") WHERE "EndedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260722113021_AddDowntimeEventOpenUniqueIndex') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260722113021_AddDowntimeEventOpenUniqueIndex', '10.0.10');
    END IF;
END $EF$;
COMMIT;

