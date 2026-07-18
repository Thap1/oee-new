---
title: Review — ARCHITECTURE-SPINE.md (oee-new)
reviewed_file: ../ARCHITECTURE-SPINE.md
sources_checked:
  - ../../../prds/prd-oee-new-2026-07-17/prd.md
  - ../../../prds/prd-oee-new-2026-07-17/addendum.md
review_date: 2026-07-17
verdict: pass-with-findings
---

# Review — Architecture Spine (oee-new)

## Verdict

**Pass-with-findings.** The spine is structurally sound, its ADs are individually well-formed (Binds/Prevents/Rule triples), the stack table is verified-current (checked live, see below), and FR/NFR coverage is nearly complete. However there is one **unresolved internal contradiction between AD-4 and AD-7** that can break a stated NFR (offline/network-fault tolerance) in exactly the scenario the ADs claim to protect, plus a **materially thin operational/environmental envelope** (no backup/DR, no release/rollout mechanism for N distributed on-prem servers, no observability, no non-prod environments). Both should be resolved (or explicitly moved to "Deferred" as acknowledged open questions) before this spine is relied on as "final."

---

## 1. Real divergence points — coverage

The spine correctly identifies and fixes the primary divergence risks for a "replicated modular monolith + central sync" system:

- Layering discipline within each instance (AD-1)
- Site autonomy vs. central aggregation split (AD-2)
- Protocol-abstraction boundary for machine ingestion (AD-3)
- Master-data ownership direction (AD-4)
- Cross-site report comparability via fixed taxonomy (AD-5)
- ID generation strategy to avoid PK collision on sync (AD-6)
- Auth/identity split between central issuance and site-local offline verification (AD-7)
- Real-time transport choice (AD-8)

These are the right categories of divergence for this paradigm. Two categories are **not** fixed and should be:

- **i18n mechanism** (see §6, Finding 3) — the Consistency Conventions table leaves the FE i18n library as an open "either/or" (`Angular i18n hoặc ngx-translate`), which is exactly the kind of decision a spine exists to pin down, since two feature teams (dashboard vs. reports vs. master-data) picking different libraries is a real, working-integration-breaking divergence, not a stylistic one.
- **Operational envelope** (see §5) — deployment topology is described, but the mechanics of running that topology safely across N independent on-prem sites (backup/DR, release rollout, monitoring) are silent, not even deferred.

No FR/NFR is entirely ungoverned (see §4 for the full map), but two FRs (FR-006, FR-017) are governed in practice only by a generic NFR-5 reference and are not cited in the AD-7 `Binds` field or in the Capability→Architecture Map rows that should list them — a traceability gap rather than a governance hole (see Finding 4).

---

## 2. AD-by-AD Rule vs. Prevents check

| AD | Rule genuinely blocks the Prevents scenario? | Notes |
| --- | --- | --- |
| AD-1 (Layered monolith) | Partially | The Rule (Domain⊥Infra/Api, Application→interfaces only, Api thin) does stop business logic leaking into controllers and does keep Domain independently testable — that half of "Prevents" is solid. But it does **not** actually stop "site xây kiến trúc khác nhau (microservices ở site này, monolith ở site khác)" by itself; that guarantee really comes from AD-2's "mỗi site có Postgres DB riêng" implying one deployable per site. Combined, adequate; AD-1 alone is a layering rule, not a topology rule. Also purely convention-enforced — no mention of an automated boundary check (e.g., architecture/dependency test), so "enforceable" today means code-review discipline only. (Low finding, §6.5) |
| AD-2 (Site autonomy) | Yes | Local Postgres per site + full daily operation without central connectivity + periodic aggregate-only sync directly prevents both stated failure modes. Solid. |
| AD-3 (Ingestion adapter) | Yes | Clean interface boundary (`IProductionDataSource`) genuinely isolates protocol churn from Domain/Application. Solid. |
| AD-4 (Site-owned master data) | Yes, in isolation — but see AD-7 conflict | Rule correctly states site-local write, central read-only via Sync. In isolation this prevents the stated SPOF/config-bottleneck scenario. **However it collides with AD-7's authentication flow** — see Finding 1 (Critical). |
| AD-5 (Reason code taxonomy) | Yes | Mandatory mapping to one of 3 fixed global values is a real, DB/domain-enforceable constraint that directly enables cross-site comparability. Solid. |
| AD-6 (GUID for synced entities) | Yes | Client-generated GUID at site-of-creation is the standard, correct fix for the stated PK-collision scenario. Note: the entity list ends in "..." and does not explicitly include `User`, even though AD-4 says `User` is one of the entities central reads via Sync — worth making explicit given Finding 1 hinges on how User records reach central. |
| AD-7 (Central identity, offline-first auth) | Partially — internally inconsistent, see Finding 1 | JWKS caching for signature verification without a round-trip is a sound, enforceable mechanism for "already-issued tokens survive site network loss." But the Rule does not explain how a **central** IdP authenticates (issues a first token for) a user whose account record is, per AD-4, created and owned at the site and reaches central only via periodic, non-real-time Sync. |
| AD-8 (SignalR per site) | Yes | Single hub per site instance, FE subscribes to it — directly prevents both polling-latency and mechanism-fragmentation scenarios. Solid. |

---

## 3. Stack table — verified-current check

Live web search was used to check each claim (all dates below are as reported in current sources; today's date in this environment is 2026-07-17):

| Claim in spine | Verification result |
| --- | --- |
| .NET 10.0 LTS, "hỗ trợ tới 11/2028" | Confirmed: .NET 10 is LTS, officially supported through **November 10, 2028** per Microsoft's .NET blog / support policy. Spine's "11/2028" is correct (month-level rounding). |
| Angular 22.0 ("mới nhất đã verify: 22.0.6, 7/2026") | Confirmed: Angular 22 went stable June 3, 2026; **22.0.6 / 22.0.7 were the current patch releases in July 2026**, matching the spine's claim almost exactly. |
| PrimeNG 22.0.0 ("PrimeNG vừa chuyển sang tổ chức PrimeUI, repo cũ đã archive") | Confirmed, and notably specific: the `primefaces/primeng` GitHub repo was **archived on June 28, 2026**, with development continuing under a new PrimeUI organization/foundation. This is a real, dated, verifiable event — not a hallucination — and a good example of the spine correctly flagging a supply-chain risk (package/org rename) that an implementer needs to know at scaffold time. |
| PostgreSQL 18.4 | Confirmed: PostgreSQL 18.4 (along with 17.10/16.14/15.18/14.23) was released **May 14, 2026**, consistent with "bản minor mới nhất của major 18." |
| SignalR bundled with ASP.NET Core 10 / EF Core 10.0 | Consistent with .NET 10 versioning; not independently disputed by any source found. |

**Conclusion: the stack table is accurate and current, not stale or hallucinated.** This is a genuine strength of the document — it reflects real, dated ecosystem events (PrimeNG's archival/reorg) rather than generic version-bumping.

---

## 4. Capability → Architecture Map vs. PRD FR/NFR groups

| PRD item | Spine coverage | Gap? |
| --- | --- | --- |
| FR-001..003 (ingestion) | AD-3 | None |
| FR-004, FR-005 (real-time dashboard, color state) | AD-8 | None |
| FR-006 (Manager/Viewer multi-site/line dashboard, permission-scoped) | Implied by AD-7's generic authorization rule, but **not cited** in AD-7's own `Binds` list or in the Dashboard row of the Capability Map (which lists only AD-8, AD-6) | Traceability gap (Finding 4, low/medium) |
| FR-007 (bilingual UI) | i18n row in Consistency Conventions, but mechanism left as "Angular i18n hoặc ngx-translate" | Ambiguity, not absence (Finding 3, medium) |
| FR-008..010 (downtime/reason code/quality reject) | AD-5 | None |
| FR-011..015 (master data & permissions) | AD-4, AD-7 | Governed, but see Finding 1 — the combination itself is where the unresolved tension lives |
| FR-016..018 (reports) | AD-2, AD-5 | FR-017 (permission-scoped filtering) same traceability gap as FR-006 |
| FR-019..021 (pie chart, Equipment/Area, drill-down) | AD-8, AD-6 | None |
| NFR-1 (real-time) | AD-8 | None |
| NFR-2 (multi-site) | AD-2 | None |
| NFR-3 (on-prem deployment, periodic sync) | AD-2 + Deployment & Environments section | Section is thin — see Finding 2 |
| NFR-4 (network fault tolerance at site) | AD-2, AD-7 | Undermined by Finding 1 in the specific case of newly-created site users needing to authenticate |
| NFR-5 (authZ enforced server-side, not just UI) | AD-7 (explicit: "không chỉ ẩn/hiện UI") | None — this is the one NFR the spine states most crisply |
| NFR-6 (bilingual) | Same i18n row as FR-007 | Same ambiguity |

No FR/NFR is entirely ungoverned. The gaps found are (a) one real internal contradiction, (b) one left-open either/or that should be a single decision, and (c) traceability/citation gaps rather than missing governance.

---

## 5. Feature-altitude dimension coverage (the operational/environmental envelope)

The checklist asks specifically whether deployment & environments, infra/provider strategy, and operations are covered — not just domain/logic concerns. Assessment:

**Covered:**
- Physical topology: one on-prem server + local Postgres per site, one central instance (same binary, `AppMode=Central`), Identity Provider co-located with central. This is a real, if brief, "Deployment & Environments" subsection.

**Not covered, and not even listed in "Deferred" as an acknowledged open question:**
- **Backup / disaster recovery** for the per-site on-prem Postgres instance. Given AD-2 makes each site's local DB the sole authoritative store for all real-time production/downtime/quality data between sync cycles, and NFR-3 explicitly commits to on-prem infrastructure at each factory (subject to ordinary hardware failure, power loss, disk failure), the total silence on backup/restore/DR strategy is a meaningful gap for a system whose whole selling point is "don't lose shop-floor data."
- **Release/update rollout mechanism** across N independently-deployed on-prem site servers plus the central instance running "cùng binary." Nothing addresses how a new version reaches site servers (manual visit? remote push? does every site run the same version at the same time, or can they drift?) — yet AD-6/AD-2's sync design implicitly assumes some level of schema/version compatibility across sites and central.
- **Observability**: no logging, monitoring, or health-check convention for detecting a silently-down site (as distinct from FR-003's product-level "machine stopped sending data" alert, which is a different, already-covered concern). Operationally, how does anyone find out that Site B's server has been offline for three days?
- **Non-production environments**: no mention of dev/local/staging environments or how multiple teams building against this spine in parallel get a working local loop (local Postgres instance? seed data? containerized dev stack?). This is normally within "feature altitude" scope for a spine meant to keep independently-built units consistent.

This silence is a finding in its own right (Finding 2), independent of any single AD, because the checklist explicitly calls out this dimension and the document does not decide, defer, or even flag it as an open question — it is simply absent.

---

## 6. Findings

### Finding 1 — CRITICAL: AD-4 and AD-7 are not reconciled; central IdP cannot authenticate a just-created site-local user

- **Where:** AD-4 ("Master data thuộc sở hữu của từng site") vs. AD-7 ("Identity tập trung, xác thực offline-first tại site").
- **The contradiction:** AD-4 states `User` (Operator/Manager/Viewer, local accounts) is data **written at the site**, never written at central, and central only ever *reads* it, via the periodic (explicitly non-real-time) Sync module. AD-7 states "Một Identity Provider trung tâm phát hành JWT" — a single central Identity Provider issues all JWTs — and that "Đăng nhập lần đầu (cấp token mới) yêu cầu site liên lạc được trung tâm" (first login / new-token issuance requires the site to reach central).
- **Why this actually breaks something:** if a site Admin creates a new Operator account locally (exactly the scenario AD-4's own `Prevents` clause is designed to enable — "site không tự thêm được máy/user khi cần gấp"), that user record exists only at the site until the next periodic Sync cycle. But per AD-7, the *first* login for that new user needs the *central* IdP to issue a token — and the central IdP has no way to validate credentials for a user it doesn't know exists yet. The new Operator is locked out until (a) a sync cycle has propagated the user record to central, and (b) the site has connectivity to central at the moment of first login. This directly undermines both AD-4's own stated purpose and NFR-4 (downtime-recording must not depend on connectivity to the outside).
- **What's missing:** the spine needs to state explicitly one of: (a) credential validation for site-local roles happens locally at the site (only *token issuance format/signing* is centralized, e.g. via a shared signing key or a site-embedded issuer that mints tokens using centrally-distributed key material) — which would require rewriting AD-7's "Một Identity Provider trung tâm phát hành JWT" premise; or (b) new user creation triggers an immediate (not periodic-batch) push of just the credential/identity record to central, ahead of the regular aggregate Sync — which would need to be called out as an exception to AD-2's "chỉ dữ liệu tổng hợp... định kỳ" rule; or (c) first login for site-local roles is explicitly out of scope for "must work offline" and is accepted as requiring connectivity, in which case NFR-4's scope should be narrowed to say so.
- **Failure scenario:** Factory site loses its link to the head office (a normal, expected condition per NFR-3/NFR-4). The site Admin, working entirely locally per AD-4, onboards a new Operator for an urgent shift-coverage need. The Operator cannot log in — because AD-7 requires central for "cấp token mới" — until connectivity is restored *and* the periodic sync (interval undefined, deferred) has run. This is the exact "site ngừng hoạt động khi mất kết nối liên site" failure mode AD-2 was written to prevent, now reintroduced through the identity layer.
- **Severity:** Critical — it is a real, traceable contradiction between two "final"-status ADs, not a hypothetical edge case, and it can silently block a core user journey (UJ-1's Operator) exactly under the network conditions the PRD calls out as expected (NFR-4).

### Finding 2 — HIGH: Operational/environmental envelope is largely silent, not merely deferred

See §5 for full detail. Missing, and not listed under "Deferred": backup/DR for per-site Postgres, release/rollout mechanism across N on-prem servers, observability/health-monitoring for site outages, and non-production/dev environment strategy. For an architecture whose central thesis is "on-prem, replicated, must survive being cut off," the absence of a backup/DR stance in particular is a substantive risk, not a stylistic omission — it's silent on how the system protects the one copy of shop-floor data that the whole design otherwise treats as authoritative.

### Finding 3 — MEDIUM: i18n mechanism left as an undecided either/or

- **Where:** Consistency Conventions table, i18n row: "FE dùng Angular i18n hoặc ngx-translate."
- **Why it matters:** the entire point of a Consistency Conventions table is to remove exactly this kind of independent-team divergence. Angular's built-in i18n (compile-time, `$localize`) and ngx-translate (runtime, service-based) are architecturally different approaches (compile-per-locale build vs. runtime language switch) with different implications for FR-007's "chuyển đổi được theo lựa chọn người dùng" (user-selectable runtime switch — which actually favors ngx-translate or a runtime-capable approach over Angular's classic compile-time i18n). Leaving this open invites exactly the divergence the table exists to prevent: one feature module wired for runtime switching, another requiring a full rebuild per locale.
- **Recommendation:** pick one (the FR-007 "runtime switch" requirement is itself a strong argument for a runtime-capable i18n approach) and remove the "hoặc."

### Finding 4 — LOW/MEDIUM: Traceability gaps between ADs' `Binds` fields and the FRs they actually govern

- AD-7's `Binds` lists only `NFR-5, FR-013, FR-015`, but AD-7's own rule text ("Authorization... được enforce ở tầng API... không chỉ ẩn/hiện UI") is exactly what governs FR-006 (dashboard scoped to permitted site/line) and FR-017 (report filtering scoped to permission) as well. Those two FRs are not cited there, nor are they cited in the Dashboard or Reports rows of the Capability → Architecture Map (which list only AD-8/AD-6 and AD-2/AD-5 respectively).
- This is not a governance hole — NFR-5's blanket "mọi API/màn hình" wording does technically cover it — but it is a real traceability weakness: an implementer scanning the Capability Map for "what governs the Reports module" would not be pointed at AD-7 and could plausibly ship a Reports endpoint without the policy-based authorization check AD-7 mandates.
- **Recommendation:** add `FR-006` to AD-7's Binds, and add `AD-7` to the Dashboard and Reports rows of the Capability → Architecture Map.

### Finding 5 — LOW: AD-1's layering rule is enforced by convention only

AD-1's Rule ("Domain không được reference Infrastructure hay Api...") is a sound rule but nothing in the spine specifies an automated enforcement mechanism (e.g., a dependency-direction/architecture test run in CI, such as NetArchTest or a Roslyn analyzer). As stated, the rule's enforcement depends entirely on manual code review across what is explicitly a multi-team, multi-site build — the exact condition AD-1 exists to guard against. This is a minor gap; worth a one-line addition (even just "enforced via an architecture test in CI, not just review") to make the "enforceable" claim concrete rather than aspirational. Not blocking.

---

## 7. Summary table

| # | Finding | Severity |
| --- | --- | --- |
| 1 | AD-4 (site-owned User) vs. AD-7 (central IdP, first-login requires central) unresolved — can lock out newly-created site users exactly when NFR-4 says it shouldn't | Critical |
| 2 | Operational envelope (backup/DR, release rollout across on-prem sites, observability, dev/staging environments) is silent, not deferred | High |
| 3 | i18n mechanism left as "Angular i18n hoặc ngx-translate" — undecided divergence point | Medium |
| 4 | AD-7 `Binds` / Capability Map omit FR-006 and FR-017 despite AD-7's rule governing both | Low/Medium |
| 5 | AD-1 layering rule has no stated automated enforcement mechanism | Low |

**Positive notes:** stack table independently verified as accurate and current (including the specific, dated PrimeNG→PrimeUI archival event); AD-2/AD-3/AD-5/AD-6/AD-8 are each individually well-formed and their Rules genuinely block their stated Prevents scenarios; FR/NFR coverage is essentially complete with only traceability-level gaps.
