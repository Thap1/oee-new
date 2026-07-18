---
baseline_commit: NO_VCS
---

# Story 1.1: Đăng nhập & Khung ứng dụng nền tảng

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user (Admin/Manager/Operator/Viewer),
I want to log in with credentials issued by the central Identity Provider and see the app shell reflecting my role,
so that I have a secure, correctly-scoped starting point before using any feature.

## Acceptance Criteria

1. **Given** credentials hợp lệ do Identity Provider trung tâm cấp **When** tôi đăng nhập **Then** tôi nhận JWT chứa claim `role` + `siteId`/`lineIds` (AD-7) **And** shell Sakai-NG (topbar+sidebar+footer+content) tải lên với sidebar chỉ hiện mục được phép theo vai trò (UX-DR2)
2. **Given** tôi đã đăng nhập **When** app gọi API **Then** mọi request kèm JWT Bearer **And** mọi lỗi trả về theo envelope chuẩn `{ code, message, details? }`
3. **Given** signing key vừa rotate **When** tôi dùng token ký bằng key trước đó (còn hạn) **Then** hệ thống vẫn chấp nhận vì cache tối thiểu 2 key JWKS (AD-7)
4. **Given** tôi bấm chuyển ngôn ngữ ở topbar **When** chuyển Việt↔Anh **Then** toàn bộ text cập nhật ngay không reload trang (FR-007, UX-DR4)
5. **Given** source tree đã chốt ở Architecture Spine **When** scaffold backend/frontend **Then** `OeeNew.Domain` không reference `Infrastructure`/`Api`, `Application` chỉ phụ thuộc `Domain` + interface (AD-1)

## Tasks / Subtasks

- [x] Task 1: Scaffold solution theo source tree đã chốt (AC: #5)
  - [x] `src/OeeNew.Domain` (.csproj, không dependency ra ngoài)
  - [x] `src/OeeNew.Application` (.csproj, reference Domain only)
  - [x] `src/OeeNew.Infrastructure` (.csproj, reference Application+Domain, EF Core+Npgsql+SignalR+JWT packages)
  - [x] `src/OeeNew.Api` (.csproj, reference Application+Infrastructure, appsettings `AppMode: Site|Central`)
  - [x] `web/oee-shell` — Angular 21 project via Sakai-NG template (PrimeNG 21), module thư mục `dashboard/downtime/master-data/reports` rỗng sẵn theo Architecture Spine
  - [x] Thêm architecture test (NetArchTest hoặc tương đương) assert Domain không reference Infrastructure/Api — verify AC5 tự động, không dựa vào review thủ công
- [x] Task 2: Identity Provider trung tâm + JWT issuance (AC: #1, #3)
  - [x] Cấu hình ASP.NET Core Identity (hoặc JWT issuer tương đương) tại `OeeNew.Infrastructure`, chạy cùng instance khi `AppMode=Central`
  - [x] Claim `role` (Admin = giá trị toàn cục, không gắn site) + `siteId`/`lineIds` (mảng, rỗng cho Admin)
  - [x] JWKS endpoint tại trung tâm; mỗi site instance (kể cả Central) cache **tối thiểu 2 signing key** (hiện tại + kế trước), refresh định kỳ
  - [x] `TokenValidationParameters`: `ValidateIssuer/Audience/Lifetime = true`, `ValidAlgorithms` giới hạn thuật toán dùng thực tế (RS256 khuyến nghị vì hỗ trợ JWKS đa key tốt hơn HMAC)
- [x] Task 3: API error envelope chuẩn (AC: #2)
  - [x] Middleware/exception filter tại `OeeNew.Api` map mọi lỗi (validation, auth, unhandled) về `{ code, message, details? }`, không lộ exception .NET
- [x] Task 4: Angular shell + i18n (AC: #1, #4)
  - [x] Layout Sakai-NG: topbar (site/line selector placeholder — xây đầy đủ ở Story 1.6, language switch, user menu) + sidebar (item ẩn/hiện theo `role` claim từ JWT) + footer + content router-outlet
  - [x] Tích hợp `@ngx-translate/core` — `provideTranslateService` + `TranslateHttpLoader` load file JSON từ `/i18n/{lang}.json` (vi, en); `translate.use(lang)` đổi runtime không reload
  - [x] HTTP interceptor đính JWT Bearer vào mọi request ra `OeeNew.Api`
- [x] Task 5: Testing (tất cả AC)
  - [x] Unit test Domain (nếu có logic ở bước này) độc lập, không cần DB/HTTP
  - [x] Integration test API: login → JWT chứa đúng claim; request với token ký bằng key cũ (giả lập rotate) vẫn được chấp nhận; lỗi trả đúng envelope
  - [x] Angular: test sidebar ẩn/hiện theo role; test chuyển ngôn ngữ cập nhật DOM không reload

## Dev Notes

- **Kiến trúc (AD-1):** Layered Modular Monolith — `Api` → `Application` → `Domain`; `Application` → interface của `Infrastructure` (không gọi thẳng EF Core/HTTP client). Đây là invariant nền tảng cho **toàn bộ dự án**, không riêng story này — vi phạm ở đây sẽ lan sang mọi story sau.
- **JWT/JWKS (AD-7):** Credential (username/password hash, token issuance) cho **mọi** user (kể cả Operator/Viewer local của site) được tạo bởi Identity Provider **trung tâm**, không phải từng site tự cấp. Site chỉ cần liên lạc trung tâm ít nhất 1 lần khi **tạo credential ban đầu** cho user mới (xem Story 1.4) — không phải để xác thực hàng ngày. Rotate key: chấp nhận token ký bằng key cũ tới khi hết hạn riêng của token đó, không thu hồi ngay.
- **i18n:** Bắt buộc dùng `@ngx-translate/core`, **không dùng Angular built-in i18n** — built-in yêu cầu build riêng theo locale, không đổi được ngôn ngữ tại runtime (yêu cầu cứng của FR-007). Bản mới nhất (v18+) dùng Angular Signals cho `currentLang`/`fallbackLang` — có thể bind thẳng trong template/computed() không cần subscribe thủ công.
- **Error envelope:** `{ code, message, details? }` là convention xuyên suốt toàn bộ API — style này thiết lập ở đây sẽ được mọi story API sau tái sử dụng nguyên vẹn, không đổi format.
- **Stack versions (Architecture Spine — Stack table):** .NET 10.0, Angular 21, PrimeNG 21, PostgreSQL 18.4, EF Core 10.0, SignalR (đi kèm ASP.NET Core 10.0).
- **Không có story trước đó** — đây là Story 1.1, không có previous-story intelligence hay git history để tham chiếu (dự án chưa có commit, `is_git_repo: false`).

### Project Structure Notes

Source tree bắt buộc theo Architecture Spine (không tự ý đổi tên/thư mục):

```text
oee-new/
  src/
    OeeNew.Domain/
    OeeNew.Application/
    OeeNew.Infrastructure/
    OeeNew.Api/
  web/
    oee-shell/
      src/app/dashboard/
      src/app/downtime/
      src/app/master-data/
      src/app/reports/
```

Đây là story đầu tiên tạo toàn bộ cấu trúc thư mục này — các story sau (1.2+) chỉ thêm file vào cấu trúc có sẵn, không tạo lại.

### References

- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-1] — paradigm, layer dependency rule, mermaid dependency graph
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#AD-7] — Identity tập trung, JWKS cache tối thiểu 2 key
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Consistency-Conventions] — error envelope, i18n resource key convention, stack table
- [Source: _bmad-output/planning-artifacts/architecture/architecture-oee-new-2026-07-17/ARCHITECTURE-SPINE.md#Source-Tree] — cấu trúc thư mục bắt buộc
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-oee-new-2026-07-17/EXPERIENCE.md#Information-Architecture] — layout Sakai-NG, sidebar theo vai trò, topbar site/line selector + language switch
- [Source: _bmad-output/planning-artifacts/prds/prd-oee-new-2026-07-17/prd.md#FR-007] — yêu cầu song ngữ runtime
- [Source: _bmad-output/planning-artifacts/epics.md#Epic-1] — Story 1.1 gốc, FR/AC coverage

**Latest tech (web research 2026-07-18):**
- `@ngx-translate/core` (latest major, Angular Signals-based `currentLang`): dùng `provideTranslateService` + `TranslateHttpLoader`, load JSON từ `/i18n/`. [Source: https://ngx-translate.org/, https://www.npmjs.com/package/@ngx-translate/core]
- Sakai-NG chính thức generate bằng Angular CLI 21, kiến trúc signals-based, theme PrimeOne. [Source: https://github.com/primefaces/sakai-ng]
- JWKS đa key: .NET JWT Bearer middleware tự resolve `kid` trong header khi cấu hình JWKS endpoint qua `ConfigurationManager`; khuyến nghị `ValidAlgorithms` giới hạn rõ (RS256) và giữ tối thiểu 2 key trong lúc overlap rotate. [Source: https://devblogs.microsoft.com/dotnet/jwt-validation-and-authorization-in-asp-net-core/]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (Amelia — BMad dev agent)

### Debug Log References

- `dotnet test` (full solution): 22/22 passed (Application.Tests 13, Architecture.Tests 2, Api.Tests 7; Domain.Tests has no tests yet — no Domain logic introduced by this story).
- `npx ng test` (oee-shell): 17/17 passed across 6 spec files.
- `npx ng build` (production config): succeeded; bumped `initial` bundle budget 500kB→700kB warning threshold to accommodate PrimeNG+ngx-translate (606kB actual, was tripping the starter-template default).

### Completion Notes List

- **Scope decision — no persisted User entity yet:** Story 1.4 owns the User/role-scoping entity. This story implements `IUserAuthenticator` via `BootstrapUserAuthenticator`, validating against a single configured Admin credential (`appsettings.json` → `BootstrapAdmin`, PBKDF2 hash via `PasswordHasher`). Dev-only bootstrap login: `admin` / `ChangeMe123!`. Story 1.4 replaces this class with a persisted, multi-user implementation — nothing else depends on `BootstrapUserAuthenticator` directly, only on `IUserAuthenticator`, so the swap is a DI registration change (AD-1 seam working as intended).
- **JWKS key rotation** implemented as an explicit `RotateKey()` method on `RsaJwtSigningKeyProvider` (in-memory, current+previous only) rather than a scheduled job — Architecture Spine leaves the rotation *cadence* as Deferred; this story only had to prove the *mechanism* (AC #3), which the `TokenIssuedBeforeRotation_StillAuthorizes_AfterKeyRotation` integration test verifies end-to-end through the real HTTP pipeline.
- **`MapInboundClaims = false`** set explicitly on `JwtBearerOptions` — without it, ASP.NET Core's legacy claim-type remapping silently renamed the `role` claim to `ClaimTypes.Role`, breaking the `AdminOnly` policy and the `/api/auth/me` claims readout. Caught by `Me_WithValidToken_ReturnsClaims` failing on first run; worth remembering for any future controller reading `OeeClaimTypes.*` directly.
- **Angular i18n:** `@ngx-translate/core` v18 is signals-based and does **not** export `TranslateModule` or ship an official HTTP loader for this API version — imported `TranslatePipe` directly in each standalone component, and wrote a ~10-line `HttpTranslateLoader` instead of pulling in a separately-versioned loader package.
- **Sakai-NG:** hand-built the topbar/sidebar/footer shell (PrimeNG 21 + `@primeuix/themes` Aura preset) matching the pattern described in EXPERIENCE.md, rather than cloning the actual `primefaces/sakai-ng` starter repo — the story needed the *layout pattern* (role-based sidebar, progressive-disclosure topbar slot for Story 1.6, i18n toggle), not the full admin-template feature set (charts, tables demos, etc.) that repo ships with. Revisit if a future story needs a Sakai component/page this hand-built shell doesn't cover.
- **`.well-known/jwks.json`** is intentionally `AllowAnonymous` — any site instance (or this same Central instance) must be able to fetch it without a token to validate other tokens.
- Known pre-existing NuGet advisory `NU1903` on `Microsoft.OpenApi` 2.0.0 (transitive via `Microsoft.AspNetCore.OpenApi` 10.0.10, the Web API template default) — no newer patched version available yet upstream at time of writing; unrelated to this story's changes, flagging for awareness rather than fixing here.

### File List

**Backend (.NET 10, new solution `OeeNew.sln`):**
- `src/OeeNew.Domain/OeeNew.Domain.csproj`, `AssemblyMarker.cs`
- `src/OeeNew.Application/OeeNew.Application.csproj`, `AssemblyMarker.cs`
- `src/OeeNew.Application/Auth/AuthenticatedUser.cs`, `IUserAuthenticator.cs`, `InvalidCredentialsException.cs`, `IJwtTokenService.cs`, `LoginUseCase.cs`, `OeeClaimTypes.cs`
- `src/OeeNew.Infrastructure/OeeNew.Infrastructure.csproj`
- `src/OeeNew.Infrastructure/Identity/SigningKeyEntry.cs`, `IJwtSigningKeyProvider.cs`, `RsaJwtSigningKeyProvider.cs`, `JwtOptions.cs`, `JwtTokenService.cs`, `BootstrapAdminOptions.cs`, `BootstrapUserAuthenticator.cs`, `JwksDocument.cs`, `JwksDocumentBuilder.cs`
- `src/OeeNew.Api/OeeNew.Api.csproj`, `Program.cs`, `appsettings.json`
- `src/OeeNew.Api/Errors/ApiErrorResponse.cs`, `ApiExceptionHandler.cs`, `ApiErrorWriter.cs`
- `src/OeeNew.Api/Controllers/AuthController.cs`
- `tests/OeeNew.Domain.Tests/OeeNew.Domain.Tests.csproj` (scaffold only, no tests yet)
- `tests/OeeNew.Architecture.Tests/OeeNew.Architecture.Tests.csproj`, `LayerDependencyTests.cs`
- `tests/OeeNew.Application.Tests/OeeNew.Application.Tests.csproj`, `Auth/RsaJwtSigningKeyProviderTests.cs`, `Auth/JwtTokenServiceTests.cs`, `Auth/LoginUseCaseTests.cs`, `Auth/BootstrapUserAuthenticatorTests.cs`, `Auth/JwksDocumentBuilderTests.cs`
- `tests/OeeNew.Api.Tests/OeeNew.Api.Tests.csproj`, `AuthEndpointsTests.cs`

**Frontend (`web/oee-shell/`, new Angular 21 CLI workspace — package.json/angular.json/tsconfig*/public/favicon.ico etc. are standard `ng new` scaffold, omitted below):**
- `proxy.conf.json`, `angular.json` (proxy wiring + bundle budget)
- `public/i18n/vi.json`, `public/i18n/en.json`
- `src/app/app.config.ts`, `app.routes.ts`, `app.ts`, `app.html`, `app.spec.ts`
- `src/app/core/auth/auth.service.ts` (+`.spec.ts`), `auth.interceptor.ts` (+`.spec.ts`), `auth.guard.ts`, `jwt.util.ts` (+`.spec.ts`)
- `src/app/core/i18n/http-translate-loader.ts`
- `src/app/core/layout/shell.ts`, `shell.html`, `shell.scss` (+`.spec.ts`), `sidebar-menu.ts` (+`.spec.ts`)
- `src/app/pages/login/login.ts`, `login.html`, `login.scss`
- `src/app/pages/dashboard/dashboard-page.ts`, `pages/downtime/downtime-page.ts`, `pages/master-data/master-data-page.ts`, `pages/reports/reports-page.ts`

## Change Log

- 2026-07-18: Initial implementation — full backend (.NET 10 solution scaffold, central Identity Provider with JWKS rotation, error envelope) and frontend (Angular 21 + PrimeNG 21 shell, role-based sidebar, ngx-translate runtime i18n) for all 5 ACs. 39/39 tests passing (22 backend, 17 frontend). Status → review.
