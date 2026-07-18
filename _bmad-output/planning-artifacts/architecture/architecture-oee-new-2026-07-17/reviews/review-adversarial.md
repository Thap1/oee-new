---
name: 'review-adversarial-oee-new'
type: architecture-review
reviews: '../ARCHITECTURE-SPINE.md'
method: 'adversarial pair-construction (two units, one level down)'
created: '2026-07-17'
---

# Adversarial Review — ARCHITECTURE-SPINE.md (oee-new)

## Method

For each AD, I constructed a concrete pair of implementers (two developers, or two modules built by
different people/PRs, one level below the spine) who each satisfy the AD's literal `Rule` text, and
asked: can they build something that doesn't fit together? A finding only counts if both sides can
honestly claim "I followed the Rule" — disagreements that require someone to actually break a stated
Rule are not findings here, they're just bugs.

Note on topology: AD-1/AD-2 state every site runs the *same codebase* (`AppMode: Site | Central`), so
"Site A team vs Site B team" is not always two different codebases — more often the real seam is
between two **modules/interfaces** built by different people inside that one codebase (adapter vs
Domain, Sync producer vs Central consumer, SignalR hub vs Angular client, IdP vs site User store), or
between two **pluggable implementations** of the same interface (AD-3 explicitly invites multiple
adapter implementations per protocol). The pairs below are framed accordingly.

## Findings Summary

| # | Severity | AD(s) | One-line issue |
|---|---|---|---|
| 1 | Critical | AD-4, AD-7 | "User" has two plausible owners (site master data vs central Identity Provider) with no stated reconciliation — first-login-at-site authentication path is unspecified |
| 2 | Critical | AD-4, AD-7 | AD-4's "central never writes back" is a stated intention with no enforcement mechanism; a legitimately global Admin JWT (AD-7) can call any site's write API directly, bypassing Sync entirely |
| 3 | High | AD-2, AD-6 | AD-2 says only *aggregated* data crosses Sync; AD-6's bind list names raw event entities (DowntimeEvent, ProductionCount, QualityReject) as synced — no pinned Sync payload schema/grain |
| 4 | High | AD-3 | `IProductionDataSource` fields `counter` and `status` have no defined semantics (cumulative vs delta; enum values) — two adapter implementations can silently disagree |
| 5 | High | Deferred (Sync cadence), AD-2 | Deferring exact batch interval/protocol leaves cross-site data freshness unpinned, breaking comparability of cross-site reports (FR-016..018, FR-019..021) |
| 6 | High | Deferred (JWKS rotation), AD-7 | Deferring JWKS rotation mechanics as "implementation detail" ignores that it determines whether all sites agree on token validity — a real trust-consistency invariant, not an implementation detail |
| 7 | Medium | AD-5 | Reason-code → loss-category mapping has no stated enforcement layer (DB constraint vs domain invariant vs nullable-then-backfilled) and no defined behavior for an unmapped code at Sync/report time |
| 8 | Medium | AD-6 | UUID v7 *or* v4 is allowed per record with no consistency rule — breaks any reasonable assumption of ID-order-as-time-proxy used by a paginated/incremental Sync or report |
| 9 | Medium | AD-8 | Event *names* are pinned (`MachineStatusChanged`, etc.) but event *payload shape* is not — Angular subscriber and .NET hub emitter can diverge on fields/casing with nothing to catch it |
| 10 | Low | AD-1 | Dependency-direction rule (Domain must not see Infrastructure/Api) has no named enforcement (analyzer/arch test) — it's a convention until someone adds a project reference |

---

## 1. [Critical] "User" has two plausible owners — site master data vs central Identity Provider

**AD-4** says: `Site, Line, Machine, ShiftSchedule, User (...), ReasonCode là dữ liệu ghi tại site, không
ghi tại trung tâm.` User is explicitly listed as site-owned master data.

**AD-7** says: `Một Identity Provider trung tâm phát hành JWT... Đăng nhập lần đầu (cấp token mới) yêu
cầu site liên lạc được trung tâm.` A *single central* Identity Provider issues every token, for every
role, at every site, on first login.

**The pair:**
- **Developer A** builds the master-data module per AD-4. They read "User... ghi tại site" literally:
  the `User` table (including credential/password hash) lives in the site's own Postgres, created and
  edited entirely locally, matching how Machine/Line/ReasonCode work.
- **Developer B** builds the Identity Provider per AD-7. They read "Identity Provider trung tâm phát
  hành JWT" and "đăng nhập lần đầu yêu cầu site liên lạc được trung tâm" literally: the central IdP is
  the sole authority that verifies a username/password and issues the signed token — which means it
  needs its *own* store of credentials and role/site/line claims, because a central process cannot look
  up a row that only exists in Site A's local Postgres at the moment of a Site-A login request.

Both are following their AD to the letter. But now there are two different, non-reconciled tables named
(conceptually) "User": Developer A's site-local master-data User, and Developer B's central IdP
credential store. Nothing in the spine says these are the same row, a synced replica, or two records
joined by a shared key (email? GUID?). Concretely: when a site Manager creates a new Operator locally
(AD-4, "site tự thêm được... user khi cần gấp" — explicitly called out as something AD-4 exists to
allow), how does that Operator ever get a working login, since the credential authority is central and
Sync only carries *aggregated production data* (AD-2), not master data? Either:
- every site-created User must immediately reach central to register a credential (contradicts "site tự
  trị," defeats the stated purpose of AD-4 for the offline/urgent case), or
- the central IdP and site User table are two independent things a dev could build without ever
  noticing they need to line up.

**Recommendation:** Tighten AD-4 and AD-7 together. Explicitly state: (a) whether User/credential is a
single record type with one source of truth (and which — site or central), or a two-part record
(site-owned profile + centrally-owned credential) joined by a stated key; (b) the exact provisioning
flow for "site creates an Operator while central is unreachable" — is login blocked until Sync/registration
reaches central, or does the site instance issue its own interim token? This is exactly the kind of
concurrency between "site autonomy" and "identity centralization" that needs one paragraph, not silence.

## 2. [Critical] AD-4's read-only boundary is stated intent, not an enforced mechanism

**AD-4:** `Trung tâm chỉ đọc (qua Sync), không có quyền ghi ngược xuống site.`

**The pair:**
- **Developer A** builds the Site's master-data write endpoints (`POST/PUT` for Machine/Line/User/
  ReasonCode) per AD-4 and AD-1 (Api → Application → Domain, standard CRUD + policy-based authorization
  per AD-7's Consistency Convention row).
- **Developer B** builds a Central-instance "Admin console" feature — say, "provision a new Operator
  account for Site C before its on-prem server is racked" — a legitimate, well-intentioned convenience
  feature. Since AD-7 makes `role: Admin` a **global claim**, and Consistency Conventions state
  "Policy-based authorization ở API" is what gates writes (not "which AppMode issued the request"), an
  Admin logged into the Central Angular app already holds a token that Site C's own API will accept and
  authorize for a write, per AD-7's own Rule. Developer B simply calls Site C's existing
  `POST /api/master-data/users` endpoint directly from the Central admin UI. This is not routed "qua
  Sync" at all — so it doesn't even touch the module AD-2/AD-4 talk about — and every individual Rule
  (AD-1 layering, AD-4's channel description, AD-7's global-Admin authorization) is satisfied.

The result directly contradicts AD-4's stated intent ("trung tâm... không có quyền ghi ngược xuống
site") while violating no Rule as literally written, because the Rule only describes the Sync channel,
and AD-7 independently hands out a token that is valid everywhere. There is no network segmentation,
API gate, or AppMode-aware authorization check named anywhere that would stop this.

**Recommendation:** Add a Rule to AD-4 (or a new AD-4a) that makes the boundary structural, not just
descriptive — e.g.: "Site API instances must reject master-data write requests whose caller context is
`AppMode=Central`" (a claim/header the Central instance cannot forge away), or "Site instances are not
network-reachable for inbound calls from Central at all; the only Central→Site data path is the site
pulling JWKS." Either is fine — but *something* enforceable needs to exist, because right now the only
thing stopping this pair is that both developers happened not to think of it.

## 3. [High] No pinned Sync payload schema/grain — AD-2 vs AD-6 disagree on what syncs

**AD-2:** `Chỉ dữ liệu đã tổng hợp (không phải raw event) đi qua module Sync... theo chu kỳ định kỳ.`
Only aggregated (non-raw) data crosses to Central.

**AD-6 Binds:** `tất cả entity xuất hiện trong Sync module (Machine, Line, DowntimeEvent,
ProductionCount, QualityReject, ReasonCode...)` — this explicitly names **raw event entities**
(DowntimeEvent, ProductionCount, QualityReject are the raw, per-event tables per the ERD) as things that
"appear in Sync module."

**The pair:**
- **Developer A** builds the Sync producer at the site, taking AD-2 as authoritative: computes a daily
  (or shift-level) `OeeSummary` per machine — Availability%/Performance%/Quality%/OEE%, no individual
  DowntimeEvent rows — and pushes that.
- **Developer B** builds the Central reporting consumer, taking AD-6's bind list as authoritative (it's
  literally named as canon for "what's in Sync"): expects individual `DowntimeEvent` rows with GUIDs so
  Central can do the FR-019..021 pie chart *and* the "drill-down ngày" the source tree comment calls out
  for the dashboard module.

Both built a self-consistent module against a different AD. When wired together, Central either can't
render the promised drill-down (only has coarse aggregates) or Developer A's "aggregated-only" producer
silently never satisfies Developer B's consumer contract, and nobody notices until integration.

**Recommendation:** Pin one explicit Sync payload contract in the spine — name the actual entity/DTO
shape and grain that crosses the wire (e.g., "per-machine per-shift OeeSummary + per-shift
downtime-by-LossCategory totals; no raw DowntimeEvent/ProductionCount/QualityReject rows ever leave the
site"), and correct AD-6's bind list to stop implying those raw tables are Sync citizens if they aren't.
If cross-site daily drill-down is actually required (per the source-tree comment), say so explicitly and
size the aggregate grain accordingly — right now the two ADs point in different directions on this exact
question.

## 4. [High] `IProductionDataSource` field semantics are unpinned (`counter`, `status`)

**AD-3:** `Domain/Application chỉ biết interface IProductionDataSource (nhận: machine_id, timestamp,
counter, status — đã chuẩn hoá).` Field *names* are fixed; field *semantics* are not.

**The pair:**
- **Developer A** writes the OPC-UA adapter for a machine whose native tag is a lifetime counter; they
  pass it straight through as `counter` = cumulative count since machine boot/reset.
- **Developer B** writes the manual-entry/legacy-PLC adapter for a different machine class whose only
  available signal is "units produced this poll"; they pass `counter` = delta since last read, because
  that's the natural unit for that source and the interface never said which convention to use.

Both satisfy `IProductionDataSource` — same shape, same field name, same "đã chuẩn hoá" claim (each
adapter genuinely did normalize *from that machine's protocol*, they just normalized to a different
target semantic). The Domain OEE calculation, which must pick one interpretation, will be silently wrong
for whichever adapter didn't match its assumption — and this is exactly the failure AD-3 exists to
prevent ("phải sửa Domain/DB mỗi khi thêm một loại máy" — except here Domain doesn't need editing, it
just computes the wrong number).

Same issue on `status`: no fixed enum is named (Running/Stopped/Idle/Fault, or similar), so two adapters
can emit different string vocabularies for the same physical machine state.

**Recommendation:** Tighten AD-3's Rule to pin: (a) `counter` semantics explicitly (recommend:
cumulative, monotonic since a well-defined epoch; adapter is responsible for converting delta-native
sources), and (b) a fixed `MachineStatus` enum owned by Domain that every adapter must map into. This is
the same category of contract AD-6 already does well for IDs — extend that rigor to AD-3's payload.

## 5. [High] Deferred Sync cadence undermines cross-site report comparability

**Deferred:** `Cơ chế/tần suất chính xác của Sync module (batch interval, giao thức truyền)... quyết
định khi có hạ tầng mạng liên site thực tế.`

**The pair:**
- **Developer A** (Site A's Sync module) picks a 15-minute REST-polling push, reasoning "AD-2 only says
  'định kỳ' (periodic), nothing pins a number."
- **Developer B** (Site B's Sync module, different rollout, possibly months apart) picks a
  near-continuous message-queue flush (~1 minute latency), for the same reason.

Both satisfy AD-2's Rule ("theo chu kỳ định kỳ"). But Central's cross-site report (FR-016..018) and
cross-site pie chart (FR-019..021, AD-5) now silently compare Site A data that's up to 15 minutes stale
against Site B data that's ~1 minute stale, with no "as-of" watermark contract to even let a report
consumer *know* the two are at different freshness. For a multi-site OEE comparison, this is a
correctness problem dressed as an infrastructure detail.

**Recommendation:** Don't fully defer this. Keep transport (REST vs MQ) deferred if you like, but pin
in the spine: a shared watermark/epoch semantics for aggregated records (e.g., every synced record
carries the aggregation-window end-timestamp it represents), and a stated maximum staleness bound all
sites must meet, so Central reports can align windows correctly regardless of which transport a given
site's Sync module happens to use.

## 6. [High] Deferred JWKS rotation mechanics hide a real trust-consistency invariant

**Deferred:** `Cơ chế rotation/refresh chính xác của JWKS tại site — chi tiết implementation, không
phải invariant.`

**AD-7** only says sites "cache JWKS định kỳ" — no retention window, no multi-key overlap policy, no
revocation story.

**The pair:**
- **Developer A** (Site A's JWKS caching) implements the standard `Microsoft.IdentityModel` pattern:
  keeps a small ring of recent keys by `kid`, refreshes periodically, tolerates overlap — a
  still-unexpired token signed with the *previous* key validates fine for a good while after rotation.
- **Developer B** (Site B's JWKS caching), reading the same AD-7 Rule, implements the simpler thing that
  literally satisfies "cache JWKS": fetch-and-overwrite a single current key on each refresh. The moment
  Central rotates keys and Site B's next scheduled fetch succeeds, any still-unexpired token signed with
  the old key is now rejected at Site B — while Site A (and Central) still accept it.

Both wrote code that "caches JWKS" as AD-7 requires. The observable result: the *same* legitimately
issued, not-yet-expired Admin token is honored at one site and rejected at another during the rotation
window — directly undermining AD-7's own stated purpose ("Admin không đăng nhập được vào site khác bằng
cùng tài khoản" is literally the failure AD-7 exists to prevent, and this reproduces a variant of it).
Revocation has the identical gap in the other direction: nothing says how/whether a compromised token is
ever invalidated before natural expiry, so one dev might add an extra "check revocation online when
reachable" call (their own invention) while another doesn't, producing different revocation latency
per site.

**Recommendation:** This is not implementation detail — it's a cross-site consistency invariant and
belongs partly in AD-7 itself. At minimum, pin: a required key-retention/grace-period rule (e.g.,
"previous signing key must remain valid for verification for at least the maximum token TTL after
rotation, at every site"), and an explicit decision on revocation (e.g., "JWTs are bearer tokens with no
revocation list; keep TTL short — Xh — and rely on expiry" or "sites must check a revocation endpoint
when online, best-effort"). Leave only the transport mechanics of *how* JWKS is fetched deferred.

## 7. [Medium] Reason-code → loss-category mapping enforcement layer unspecified

**AD-5:** `mỗi ReasonCode bắt buộc map vào đúng một trong 3 giá trị cố định toàn cục.` "Bắt buộc"
(mandatory) is stated as a business rule; the spine doesn't say *where* it's enforced.

**The pair:**
- **Developer A** implements `ReasonCode` as a Domain entity whose constructor/factory requires a
  `LossCategory` argument and a `NOT NULL` DB column — you cannot persist an unmapped code.
- **Developer B**, on a different module or a later add-a-reason-code-quickly feature (motivated by
  AD-4's "site tự thêm được... khi cần gấp"), makes `LossCategory` nullable in the DB, reasoning the
  mapping is a reporting concern (only needed for FR-019 cross-site aggregation), not an operational one
  — Operators should be able to log downtime against a brand-new local reason code immediately and have
  someone map it to a category later.

Both are defensible readings of "bắt buộc" (mandatory *for reporting correctness* vs mandatory *at
creation time*). If B's reading ships, Sync/Central aggregation (AD-5's own stated purpose) hits
unmapped codes with no defined bucket — pie chart math has nowhere to put them.

**Recommendation:** Tighten AD-5 to state explicitly where the constraint lives (recommend: Domain
invariant + DB `NOT NULL`, i.e., a ReasonCode cannot exist without a category, matching Developer A's
reading) and, separately, define what Sync/reporting does if a legacy/unmapped record is ever
encountered (reject at Sync, or route to a reserved "Unclassified" bucket) — don't leave it to whichever
dev's instinct wins.

## 8. [Medium] UUID v7-or-v4 choice-per-record breaks implicit ID-ordering assumptions

**AD-6:** `dùng Guid (UUID v7 hoặc v4) làm khoá chính` — either version is explicitly allowed, with no
consistency rule about which producer must use which.

**The pair:**
- **Developer A** (Central reporting/Sync-consumption module) reasonably assumes UUID v7's
  time-orderable prefix to build an efficient incremental-sync watermark or a "recent records" query
  (`ORDER BY Id DESC LIMIT n` as a cheap proxy for recency, or index range scans for pagination).
- **Developer B** (a Site's adapter/master-data module, or another site entirely) generates UUID v4 for
  the same entity types, because AD-6 explicitly permits it ("v7 hoặc v4") and v4 is simpler to generate
  with the platform's default `Guid.NewGuid()`.

Both satisfy AD-6's Rule to the letter. Developer A's optimization/assumption silently breaks (wrong
ordering, index fragmentation, incorrect "latest N" queries) for every record that happens to come from
a v4 producer, and there's no way to tell from the schema which producer used which scheme.

**Recommendation:** Either pin one scheme uniformly (recommend UUID v7 everywhere, since Postgres 18 and
.NET 10 both support it natively and it's strictly better for insert locality) or, if flexibility is
truly wanted, add an explicit Rule stating "no code may rely on GUID value ordering as a time or
recency proxy" so Developer A's approach is ruled out up front instead of silently wrong.

## 9. [Medium] SignalR event names are pinned; payload shape is not

**Consistency Conventions:** `tên event dạng MachineStatusChanged, DowntimeReasonRecorded` — names are
fixed, but no field list/DTO is given (contrast with AD-3, which does enumerate exact fields for the
ingestion interface).

**The pair:**
- **Developer A** (the .NET SignalR hub) emits `DowntimeReasonRecorded` with whatever fields the
  Application-layer use case object happens to expose — say, `{ MachineId, ReasonCodeId, StartedAtUtc }`.
- **Developer B** (the Angular dashboard) is built against what the FR/UX mockup implies is needed for
  the live pie-chart tile — say, expecting `{ machineId, lossCategory, reasonLabel, timestamp }` so it
  can update the chart without an extra lookup round-trip.

Both are "following AD-8" (same hub, same event name, real-time within seconds). But the payload
contract was never pinned anywhere, so the two sides can easily diverge on which fields are present,
whether `LossCategory` is resolved server-side or left for the client to join against ReasonCode, and
casing conventions — discovered only at integration, per event, one at a time.

**Recommendation:** Extend the Consistency Conventions "Real-time" row (or add to AD-8's Rule) with an
explicit payload DTO per event name, the same way AD-3 pins `machine_id, timestamp, counter, status` for
ingestion.

## 10. [Low] AD-1 layering rule has no named enforcement mechanism

**AD-1:** `Domain không được reference Infrastructure hay Api... Api chỉ gọi Application, không chứa
business logic.` This is a clean, testable rule in principle, but the spine names no CI gate
(architecture unit test, Roslyn analyzer, or project-reference restriction) that would catch a violation
automatically.

**The pair:** Developer A (building an Application use case under deadline pressure) reaches for
`HttpClient` or `DbContext` directly inside `OeeNew.Application` "just this once" because it's faster
than defining a new `Application`-layer interface + `Infrastructure` implementation; nothing fails a
build or a PR check, since the only enforcement is a sentence in a document. This is lower severity than
the others because it's a well-known, generically-solvable problem (not specific to this domain), but
worth naming since the spine otherwise goes out of its way to make Rules mechanical.

**Recommendation:** Name a concrete enforcement mechanism (e.g., an `ArchUnitNET`/`NetArchTest` test
that asserts `OeeNew.Domain` has zero project references beyond itself, run in CI) so AD-1 stops being
purely a convention.

---

## What this review deliberately did not flag

- Stack/version choices (Consistency Conventions, Stack table) — these are shared literally, not
  independently implemented, so there's no "two units diverge" story there.
- The Deferred items for machine-protocol adapters, alerting, quality/SPC module, timeline, and UX
  detail — these are genuinely fine to defer; no pair of implementers building *other* modules is
  forced to guess at their shape today.
