# Deferred Work

## Deferred from: code review of 1-2-site-line-machine-management (2026-07-19)

- No uniqueness constraint/check for Site/Line/Machine names (app or DB level) — not required by any AC in this story; revisit if duplicate names become a real problem. [src/OeeNew.Domain/MasterData/Site.cs, src/OeeNew.Infrastructure/Persistence/OeeDbContext.cs]
- Integration tests (`MasterDataApiFactory`) require a real local Postgres `oeenew_test` instance with hardcoded credentials, no docker-compose/testcontainers/CI provisioning, and never clean up inserted rows — needs a project-level test-infra decision (e.g. testcontainers) broader than this story. [tests/OeeNew.Api.Tests/MasterData/MasterDataApiFactory.cs]
- List endpoints (`sites`, `lines`, `machines`) are entirely unpaged — acceptable at current master-data volumes, revisit if that assumption changes. [src/OeeNew.Api/Controllers/SitesController.cs, LinesController.cs, MachinesController.cs]
- Angular bundle budget raised instead of lazy-loading the master-data route — acceptable now, revisit lazy-loading as more PrimeNG-backed features land. [web/oee-shell/angular.json]
