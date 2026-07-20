# Deferred Work

## Deferred from: code review of 1-2-site-line-machine-management (2026-07-19)

- No uniqueness constraint/check for Site/Line/Machine names (app or DB level) — not required by any AC in this story; revisit if duplicate names become a real problem. [src/OeeNew.Domain/MasterData/Site.cs, src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs]
- Integration tests (`MasterDataApiFactory`) require a real local Postgres `oeenew_test` instance with hardcoded credentials, no docker-compose/testcontainers/CI provisioning, and never clean up inserted rows — needs a project-level test-infra decision (e.g. testcontainers) broader than this story. [tests/OeeNew.Api.Tests/MasterData/MasterDataApiFactory.cs]
- List endpoints (`sites`, `lines`, `machines`) are entirely unpaged — acceptable at current master-data volumes, revisit if that assumption changes. [src/OeeNew.Api/Controllers/SitesController.cs, LinesController.cs, MachinesController.cs]
- Angular bundle budget raised instead of lazy-loading the master-data route — acceptable now, revisit lazy-loading as more PrimeNG-backed features land. [web/oee-shell/angular.json]

## Deferred from: code review of 1-1-login-app-shell (2026-07-20)

- Central Identity Provider isn't actually gated by `AppMode` — every instance runs its own local signing-key provider/JWKS endpoint regardless of `Site`/`Central` mode; no cross-instance federation exists yet. [src/OeeNew.Api/Program.cs:16-25, src/OeeNew.Infrastructure/Identity/RsaJwtSigningKeyProvider.cs:18-21]
- Signing keys are in-memory only and unique per process — a restart invalidates all outstanding tokens, and no multi-instance key sharing exists. [src/OeeNew.Infrastructure/Identity/RsaJwtSigningKeyProvider.cs:18-21]
- `RotateKey()` has no caller anywhere outside tests — no scheduled job or admin trigger exercises the rotation-overlap window in production. [src/OeeNew.Infrastructure/Identity/RsaJwtSigningKeyProvider.cs:39]
- Only the bootstrap Admin account can log in through the real pipeline; other roles are validated only against hand-built fake JWTs, not a real end-to-end login. Superseded by Story 1.4. [src/OeeNew.Infrastructure/Identity/BootstrapUserAuthenticator.cs]
- No client-side per-route role guard — only the sidebar hides links; a non-Admin user can still navigate directly to `/master-data` by URL (server-side enforcement is the real boundary per NFR-5). [web/oee-shell/src/app/core/auth/auth.guard.ts]
- 8-hour bearer token stored in `localStorage` with no refresh-token pattern or server-side revocation. [web/oee-shell/src/app/core/auth/auth.service.ts:47]

## Deferred from: code review of 1-3-shift-schedule-management (2026-07-20)

- Overlap check is read-then-write with no DB-level constraint — two concurrent creates for the same Site/Line could both pass validation and insert overlapping shifts (AC #2 race). Same class of gap as Story 1.2's deferred uniqueness-constraint item; low-probability given Admin-driven manual usage. [src/OeeNew.Application/MasterData/ShiftScheduleManagementUseCase.cs:61-69, src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs:51-66]
