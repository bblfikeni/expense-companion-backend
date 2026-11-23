# PRODUCT REQUIREMENTS DOCUMENT
## SARS Expense Companion (MVP)

- **Version:** 1.0 (draft)
- **Date:** 2026-11-23
- **Supersedes:** Brief Document; TRS v1.2 where they conflict (see §15)
- **Owner:** Product / Architecture (user); execution by specialised coding agents

### Confidence legend

| Tag | Meaning |
|---|---|
| **[Decided]** | Stated by the user as a locked decision |
| **[Reported]** | Reported by a coding agent, not verified against code |
| **[Assumed]** | Inference or default proposed in this document. Needs confirmation |
| **[Open]** | No decision exists |

---

## 1. Product summary

A mobile-first PWA for South African freelancers, sole proprietors and small businesses to capture receipts, organise them into categorised expense records with VAT breakdowns, track missing proof, and produce reports for their accountant or year-end handoff. **[Decided]**

**Core promise:** "Keep every slip. Claim what's yours. Be audit-ready." **[Decided]**

## 2. Positioning & legal boundaries (non-negotiable)

These must appear in every agent-facing output and must never be softened. **[Decided]**

- NOT a SARS filing tool (no eFiling submission)
- NOT accounting software (no double-entry, ledgers, balance sheets)
- NOT tax advice (category suggestions are organisational only)
- No claims of SARS endorsement or affiliation
- No OCR accuracy guarantees
- No App Store / Play Store language (PWA only)

## 3. Target users

| Segment | Status |
|---|---|
| Freelancers and sole proprietors | **[Decided]** |
| Small businesses (Pty Ltd, CC) | **[Decided]** (`business_type` enum in TRS v1.2) |
| Accountants / bookkeepers as users | **[Open]**. They are report recipients only, with no accounts, unless decided otherwise |

## 4. Goals and non-goals

### Goals
1. Receipt to confirmed expense record in under one minute on a mid-range phone on a South African cellular connection. **[Assumed]** (target value not stated)
2. Zero lost receipts: capture must survive network failure. **[Decided]** (data-loss-prevention lifecycle)
3. Clear, always-visible "missing proof" state. **[Decided]**
4. Lighthouse performance ≥ 90 at a 375px baseline. **[Decided]**
5. POPIA-aligned from registration onward. **[Decided]**

### Non-goals (MVP)
- Filing, submission or reconciliation with SARS
- Bank feed integration, invoicing, payroll **[Assumed]**
- Native iOS/Android apps
- Receipt storage / cross-border POPIA review (deferred until after MVP) **[Decided]**
- 3-state theme toggle (system/light/dark) **[Decided]**
- Per-workspace notification channel preferences **[Decided]**

## 5. MVP scope

### 5.1 Website pages in scope **[Decided]**
Homepage, Registration, Login, Dashboard, Gallery. The website is a **new build**, frontend-only, shipped with mock data and services so look and feel can be reviewed before the backend exists.

### 5.2 Required but not in the listed page set **[Open]**
Capture flow, Export/Reports, Account/Privacy, Billing/Upgrade, Email verification and password reset screens. The frontend inventory shows some as existing (mock). Confirm which are MVP-blocking.

## 6. Functional requirements

### 6.1 Identity & access

| ID | Requirement | Status |
|---|---|---|
| ID-01 | Registration with email and password; POPIA consent field is mandatory on `RegisterRequest`; consent timestamp stored | **[Decided]** |
| ID-02 | Email verification via real OTP/magic-link flow (not auto-verify) | **[Decided]** |
| ID-03 | Login issues JWT; refresh tokens persisted with revocation state | **[Decided]** |
| ID-04 | Logout revokes server-side (not client-discard only) | **[Decided]** |
| ID-05 | Forgot / reset password via email token | **[Decided]**; slice implemented **[Reported]** |
| ID-06 | Profile get/update | **[Reported]** |
| ID-07 | Security-stamp check on every authenticated request | **[Reported]** |
| ID-08 | RBAC via ASP.NET Core Identity, int PKs, permission codes; platform admin = `platform-admin` role with `platform:*` claim, not a boolean | **[Decided]** |
| ID-09 | Social login (Google/Microsoft) | Buttons exist in mock UI. Backend **[Open]** |
| ID-10 | Account deletion / POPIA erasure (anonymisation pipeline) | In TRS v1.2 only; not in locked decisions. **[Open]** |
| ID-11 | Workspace roles (owner/member) for Premium multi-user (max 5) | **[Open]**. RBAC decisions cover platform level only |

### 6.2 Workspace

| ID | Requirement | Status |
|---|---|---|
| WS-01 | Workspace has company name, business type, VAT-registered flag, VAT number (validated when flagged) | TRS v1.2; **[Assumed]** still valid |
| WS-02 | Free: single-user workspace. Premium: up to 5 members | **[Decided]** |

### 6.3 Receipt capture & ingestion

| ID | Requirement | Status |
|---|---|---|
| CP-01 | Capture via camera (mobile browser) or upload; multi-page receipts supported | Mock built; **[Decided]** direction |
| CP-02 | Client-side compression to WebP before upload | **[Decided]** |
| CP-03 | Client generates tracking ID and initialises a draft record before upload (state safeguard against network failure) | **[Decided]** (TRS v1.2 §5.1) |
| CP-04 | Client uploads directly to Cloudflare R2 via short-lived presigned URL | **[Decided]** |
| CP-05 | After upload, client signals the gateway; expense moves to `Processing`; gateway publishes event via transactional outbox | **[Decided]** |
| CP-06 | Client shows distinct Processing / Extracted / Failed states; refetches on load and reconnect; polling fallback if realtime drops | **[Decided]** |
| CP-07 | Captured photos must survive tab close/refresh before upload completes | **[Open]**. Current mock holds photos in tab memory only |
| CP-08 | WhatsApp inbound capture (webhook, phone-to-workspace mapping, same pipeline) | Planned, post-MVP. **[Decided]** as future scope |

### 6.4 Extraction

| ID | Requirement | Status |
|---|---|---|
| EX-01 | Extract: vendor, transaction date, total, VAT amount, VAT number, "Tax Invoice" presence, per-extraction confidence | **[Decided]** |
| EX-02 | Reject pro-forma / quote / estimate as invalid tax invoices | **[Decided]** |
| EX-03 | Support English and Afrikaans, faded/skewed thermal receipts | **[Decided]** |
| EX-04 | Self-hosted open-source OCR preferred; no data leaving own infrastructure | **[Decided]** preference; engine **[Open]** (research in progress) |
| EX-05 | Server-side contrast clean-up and thumbnails via SixLabors.ImageSharp | **[Decided]** |
| EX-06 | Consumer is idempotent with retry limits and dead-letter queue; writes result columns only (user-entered data wins); uses a narrow DB role | **[Decided]** |
| EX-07 | Low-confidence results route to manual entry; `entry_origin` records System_OCR / User_Manual / User_Override | **[Decided]** (schema); confidence threshold **[Open]** |
| EX-08 | Stuck-`Processing` sweeper: 5-minute timeout, 3 retries, runs in the standalone Scheduler service | **[Decided]** |

### 6.5 Expense management

| ID | Requirement | Status |
|---|---|---|
| EM-01 | Expense record: date, vendor, amount, VAT, VAT classification, SARS category code, receipt status, validity flag, duplicate flag, entry origin, confidence, storage reference | TRS v1.2 schema; see §9 for superseded fields |
| EM-02 | VAT classes: Standard 15%, Zero-rated, Exempt, Not claimable, Second-hand | TRS v1.2; **[Assumed]** retained |
| EM-03 | User confirms extracted data on a "Confirm Card" before the expense is saved | **[Decided]** (TRS v1.2) |
| EM-04 | Duplicate detection flag | TRS v1.2; matching logic **[Open]** |
| EM-05 | Category suggestions are informational only | **[Decided]** |
| EM-06 | Free-tier cap of 20 expenses/month enforced at confirm/save, not at capture initiation | **[Decided]** |
| EM-07 | Missing-proof checks surfaced on dashboard and gallery | **[Decided]** (homepage feature) |

### 6.6 Dashboard & gallery

| ID | Requirement | Status |
|---|---|---|
| DB-01 | Dashboard: expense totals, VAT totals, missing-proof count, tax-year switcher | **[Decided]** direction; exact metrics **[Open]** |
| GL-01 | Gallery: receipt grid with category filter chips, lazy-loaded WebP thumbnails | **[Decided]** |
| GL-02 | Filter/sort state held in URL (Nuqs) | TRS; **[Assumed]** retained |

### 6.7 Reporting & export

| ID | Requirement | Status |
|---|---|---|
| RP-01 | Ad-hoc report request: client → gateway validates → publishes to RabbitMQ → Reporting node generates | **[Decided]** |
| RP-02 | No duplicate active report runs per user | **[Decided]** |
| RP-03 | Reports either emailed or stored for download | **[Decided]** |
| RP-04 | Scheduled (recurring) reports | **[Decided]** (Reporting node scope); UX **[Open]** |
| RP-05 | Free: PDF only. Premium: PDF + DOCX | **[Decided]** |
| RP-06 | Export must respond fast (`202 Accepted` plus task ID) and not block the client | TRS; **[Assumed]** retained |
| RP-07 | CSV export | Listed on homepage, built in mock, **conflicts with RP-05**. **[Open]** |

### 6.8 Notifications

| ID | Requirement | Status |
|---|---|---|
| NT-01 | Resend is the sole email provider in all environments | **[Decided]** |
| NT-02 | Notification service is event-driven, consumes only from RabbitMQ; the web client has no direct access | **[Decided]** |
| NT-03 | Channel abstraction (`INotificationChannel`; Email, WhatsApp) with per-channel on/off flags | **[Decided]**; not built |
| NT-04 | Realtime (SignalR, with backplane) pushes processing results to the client; it is a speed-up, DB is the source of truth | **[Decided]** |
| NT-05 | Auth emails (verification, reset) carry OTP/token in plaintext in RabbitMQ events | **[Decided]**. See risk R-4 |
| NT-06 | Reminders: Premium only | **[Decided]** |

### 6.9 Billing

| ID | Requirement | Status |
|---|---|---|
| BL-01 | 30-day full-featured trial, then auto-downgrade to Free (not cutoff) | **[Decided]** |
| BL-02 | Premium via PayFast with recurring billing token | **[Decided]**; not built |
| BL-03 | Downgrade overage: items beyond cap become read-only per item | **[Decided]** |
| BL-04 | Authorisation middleware enforces product entitlement on every request; billing lives in gateway or its own node | **[Decided]** (placement slightly ambiguous in source; confirm) |
| BL-05 | Pricing: Free R0 / Premium R149 per month | Hardcoded deliberately; **not final**. Revisit before public launch |

### 6.10 Marketing homepage **[Decided]**

Sections in order: header (brand mark, light/dark toggle, Sign in, Get started) → hero → capture-to-report animation → feature list → example expense (Karoo Hardware, R818.80) → pricing → FAQ and trust notes → closing CTA ("Start keeping slips") and footer with legal disclaimers.

## 7. Non-functional requirements

| Area | Requirement | Status |
|---|---|---|
| Performance | Lighthouse ≥ 90; RSC, edge rendering, aggressive image compression | **[Decided]** |
| Viewport | 375px baseline; larger breakpoints | Baseline **[Decided]**; others **[Open]** |
| Security | Server-side token revocation; no signing keys or peppers in config (required at startup); CORS restricted to the client origin only; separate `PublicId` GUIDs exposed, int PKs never in API/JWT/URLs | **[Decided]** / **[Reported]** |
| Reliability | Transactional outbox, DLQ, idempotent consumers, Polly retries and circuit breakers, health checks gated on migration completion | **[Decided]** |
| Observability | Serilog to self-hosted Seq; `X-Correlation-Id` across HTTP and RabbitMQ; consistent error schema | **[Decided]** |
| Privacy | POPIA: consent capture, erasure, South African data handling claims | Consent **[Decided]**; rest see R-2 |
| Testing | xUnit + WebApplicationFactory (Testcontainers PG17) + Schemathesis per phase; Swagger/OpenAPI on all services | **[Decided]** |
| Accessibility | Reduced-motion fallback exists. No conformance target | **[Open]**. Proposed: WCAG 2.2 AA **[Assumed]** |

## 8. Architecture constraints (summary)

- Client-server, strictly decoupled; API contract is the only dependency between the web agent and backend agent. **[Decided]**
- Single shared PostgreSQL 17 database, flat schema; EF Core migrations in `Companion.Data`, applied at gateway startup. **[Decided]**
- **Services:** Gateway API (identity + expense + billing entitlement; never consumes RabbitMQ), Extraction, Notification, Realtime (SignalR), Reporting, Scheduler. **[Decided]**
- Solution layout: `Companion.Core`, `.Data`, `.Events` (models only, no logic), `.Messaging` (EasyNetQ, default conventions), `Services/*`, `Tests/Companion.Api.Tests`, `web/` submodule. **[Decided]**
- All RabbitMQ access via `Companion.Messaging`. **[Decided]**
- Native AOT is the target. **[Decided]**; QuestPDF and Open XML SDK AOT compatibility **unverified** (spike required before reporting architecture is committed).
- Local: `dotnet run` against local Postgres/RabbitMQ/Seq; `appsettings.Development.json` gitignored. Deployment: docker-compose with `secrets/` pattern. **[Decided]**

## 9. Data & contract rules

- **Money:** `long` cents (`AmountInCents`) end to end; rand conversion in the UI only. **[Decided]**
- **Receipt status:** `None / Uploaded / Processing / Qualified / Rejected` (backend-canonical). **[Decided]**
- **IDs:** int PK internally; `PublicId` (Guid, unique) externally. **[Decided]**
- Backend is canonical for DTOs, enums, field names and units; the frontend agent never asserts contracts independently. **[Decided]**
- Mock mode via `NEXT_PUBLIC_MOCK_MODE`, with a separate toggle for simulated auth failure. **[Decided]**

## 10. Design & UX direction **[Decided]**

"A trusted digital paper trail": practical, calm, precise; not glossy fintech.

- **Surfaces:** warm receipt paper on soft counter-gray; dark ink hierarchy; dotted rules; compact ledger rows
- **Type:** Bricolage Grotesque (headings); IBM Plex Mono (amounts, tax details)
- **Colour semantics:** blue = recorded/verified; red = missing proof; yellow highlight = tax/VAT detail
- **Motion:** restrained, receipt-like; reduced-motion fallback required
- **Theme:** light/dark, 2-state toggle (next-themes)
- **Language:** plain South African (rand, VAT, tax year, SARS). Never imply the app files returns or gives tax advice
- Parts of the app shell still use the superseded mint-green styling; migrate as screens are refined

## 11. Standing coverage checklist

Applied to every app build and feature plan. A row with a gap blocks "complete" status for the relevant phase.

| # | Item | Current coverage | Gap |
|---|---|---|---|
| 1 | Onboarding | Mock signup only | No first-run flow defined: workspace setup, business type, VAT status, first-capture prompt |
| 2 | Sign up and log in | Backend slice **[Reported]**; frontend mock | Verify against code; social login backend undecided |
| 3 | Email verification | OTP flow decided and **[Reported]** built | No outbox relay or email delivery, so non-functional end to end |
| 4 | Password reset | Forgot/reset **[Reported]** built | Same delivery gap as #3 |
| 5 | Account deletion | Erasure/anonymisation in TRS v1.2 only | No decision, no API, no UI. POPIA-relevant |
| 6 | User permissions | Platform RBAC decided | Workspace-level roles for 5-member Premium undefined; enforcement phase (G3) not started |
| 7 | Empty states | None specified | Define for dashboard, gallery, reports |
| 8 | Loading states | Receipt pipeline states decided | Other screens unspecified |
| 9 | Error states | Receipt `Failed` state decided | Global error, auth failure, quota-exceeded, upload failure UX unspecified |
| 10 | Network states | Polling fallback and refetch decided for receipts | Offline capture, upload resume, and PWA offline behaviour undefined |
| 11 | Data persistence | DB is source of truth; draft-before-upload decided | Mock capture holds photos in tab memory; no draft recovery after tab close |
| 12 | Payment flow | PayFast and trial/downgrade rules decided | Nothing built; pricing not final; failed-payment and dunning behaviour undefined |
| 13 | Notifications | Resend email decided; WhatsApp future | Notification content catalogue and reminder rules undefined |
| 14 | Analytics | None | No product analytics decision. Needs POPIA consent handling |
| 15 | Crash reporting | Seq for backend logs | Frontend error path undefined since the client-errors route was removed (2026-10-18) |
| 16 | Privacy set-up | POPIA consent field; privacy/terms/POPIA pages exist | Cross-border storage review deferred (see R-2); retention policy undefined |
| 17 | Accessibility | Reduced-motion fallback | No conformance target, no audit |
| 18 | Responsiveness | 375px baseline | Tablet/desktop breakpoints unspecified |
| 19 | User flows | Pipelines documented at architecture level | No end-to-end user-flow document |
| 20 | Beta testers | None | No cohort, recruitment, feedback channel or exit criteria |

## 12. Current state (as of last recorded updates)

- **Backend:** solution skeleton committed (2026-10-18). EF Core persistence and auth slice 1 **[Reported]**; 47 tests passing **[Reported]**. Not verified against code. No outbox relay, email delivery, worker logic or Realtime hubs yet.
- **Frontend:** marketing site, mock auth, dashboard, capture, gallery, reports (CSV/JSON samples), legal pages exist with mock data only. Homepage and theme direction updated 2026-11-15.
- **Phase 1 blockers (open):** JWT permission codes status; production-readiness reclassification; DB topology confirmation from config; AOT-published binary against the full endpoint matrix; `00-INDEX.md` update logging the G1/Phase 1 reorder.

## 13. Delivery approach

- Phase files (`00-INDEX.md`, hardening index, per-phase files) govern sequencing: dependency gate, tasks, review checklist, tests, exit criteria. **[Decided]**
- Every status claim is verified against code before a phase closes; agent reports have been inaccurate before. **[Decided]**
- Agent tasks sized for a single session. Prompts include scope, context, numbered steps, exclusions, verify-against-code guardrails. **[Decided]**
- Next slice: registration, forgot password, profile update, OTP verification; then G2 (rate limiting), G3 (RBAC enforcement), G4 (tooling). **[Decided]**

## 14. Risks, conflicts and open decisions

| ID | Item | Severity | Confidence |
|---|---|---|---|
| R-1 | **AOT vs. reporting libraries.** QuestPDF and Open XML SDK under Native AOT are unverified; the `SingleOrDefaultAsync` AOT break means no production claim is valid without AOT-binary tests | High | Fact (decision log); outcome unknown |
| R-2 | **Homepage claims "South African data storage"** while receipts go to Cloudflare R2 and email via Resend, and the cross-border POPIA review is deferred. A public claim you cannot yet support is a legal and trust liability. Either verify R2 region/jurisdiction and Resend data handling before launch, or remove the claim | High | High-probability inference. R2 and Resend hosting locations not verified here |
| R-3 | **Free tier vs. homepage/mock CSV export.** Decided Free = PDF only; homepage lists CSV export as a feature | Medium | Fact |
| R-4 | **Plaintext OTP/reset tokens in RabbitMQ events** (and plaintext OTP payloads in the DB with no outbox retention policy). Any broker access, log, or DLQ inspection exposes live credentials. Mitigation to consider: hash-at-rest plus short TTL plus purge on consume | High | Fact (decision); impact is an inference |
| R-5 | **Pricing R149 is unvalidated**; no cost model (OCR compute, storage, PayFast fees) behind it | Medium | Fact |
| R-6 | **OCR engine not selected**; accuracy on SA thermal receipts is the core product risk and there is no accuracy baseline | High | Assumption; research in progress |
| R-7 | **Duplicate-detection, confidence threshold and category-code mapping logic undefined** | Medium | Fact |
| R-8 | **Account deletion/erasure unspecified** despite POPIA positioning | Medium | Fact |
| R-9 | **Billing placement ambiguity:** "stays in gateway" vs. "its own node or API" | Low | Fact; confirm |
| R-10 | **Verification debt:** most backend status is agent-reported; five tests failing at last report | Medium | Fact |
| R-11 | **Capture edge case:** a delayed photo-processing callback may restore a page after Stop/Discard | Low | Fact (known frontend issue) |
| R-12 | **Frontend crash reporting gap** after the client-errors route was removed | Medium | Fact |

## 15. Superseded decisions (do not revert)

| Earlier | Now |
|---|---|
| Database-per-service | Single shared DB, flat schema |
| Four-service split (Identity, Expense, Extraction, Notification) | Gateway API + Extraction + Notification + Realtime + Reporting + Scheduler |
| UUID PKs | Int PK + `PublicId` GUID |
| `proof_status` enum (TRS v1.2) | `ReceiptStatus` enum |
| `numeric(12,2)` amounts | `long` cents |
| Mailpit local SMTP | Resend everywhere |
| No outbox for MVP (503 on publish failure) | Outbox and reconciliation sweeper in MVP |
| Notification receives client errors via HTTP route | Notification is event-driven only; web client has no access |
| TRS v1.2 docker-compose section | Misleading; do not use |
| Teal SaaS theme | "Trusted digital paper trail" theme |
| Price as placeholder | R149 hardcoded, temporary, not final |

## 16. Post-MVP backlog

WhatsApp Business inbound capture and outbound notifications; 3-state theme toggle; per-workspace channel preferences; receipt storage and cross-border POPIA review (see R-2 for the launch-gating part); pricing finalisation (launch-gating).