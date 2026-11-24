# ARCHITECTURE.md — SARS Expense Companion

Audience: coding agents implementing features. Read fully before writing code. If this document conflicts with the TRS v1.2, **this document wins**. If it conflicts with the actual code, **stop and report the conflict**; do not guess.

---

## 0. Operating Rules for Agents

1. **Verify against actual code before claiming anything.** Status reports are not evidence. Cite file paths and test output.
2. **One slice per session.** Implement only what the prompt scopes. No drive-by refactors, no extra features.
3. **Backend is canonical for contracts.** DTOs, enums, field names and units come from backend source. The frontend never invents contracts.
4. **Never silently revert an architectural decision.** If a decision looks wrong, flag it in your report and keep following it.
5. **Never soften legal positioning** (Section 1).
6. **Report honestly:** list what is built, what is stubbed, what failed, and what you did not verify.
7. **Targeted diffs over rewrites.** Do not reformat files you are not changing.
8. **Every slice ships with tests** (Section 9). Red tests are never "pre-existing, ignore".

---

## 1. Product Boundaries (never violate, never soften)

South African PWA for freelancers, sole proprietors and small businesses to capture, categorise and report expenses.

- NOT a SARS filing tool (no eFiling submission).
- NOT accounting software (no ledgers, no double-entry).
- NOT tax advice. Category and VAT outputs are suggestions only.
- No SARS endorsement claims. No OCR accuracy guarantees.
- No App Store / Play Store language. It is a PWA.
- POPIA is the compliance framework. Consent is captured at registration and persisted.

User-facing copy must not claim "South African data storage" (infrastructure is Cloudflare R2 + Resend; open item R-2).

---

## 2. Solution Layout

```
Companion/
├─ Companion.Core/          shared extensions, startup extensions, enums, DTOs
├─ Companion.Data/          EF Core (code-first, PostgreSQL), models, repositories, migrations
├─ Companion.Events/        event models ONLY. No logic.
├─ Companion.Messaging/     EasyNetQ connection strategies, generic pub/sub helpers
├─ Services/
│  ├─ Companion.Api/            gateway: identity + expenses + billing + upload orchestration
│  ├─ Companion.Extraction/     OCR / receipt processing (RabbitMQ consumer)
│  ├─ Companion.Notification/   email / WhatsApp (RabbitMQ consumer)
│  ├─ Companion.Realtime/       SignalR push (RabbitMQ consumer, backplane)
│  ├─ Companion.Reporting/      scheduled + ad-hoc report generation (RabbitMQ consumer)
│  └─ Companion.Scheduler/      all scheduled jobs incl. stuck-Processing sweeper
├─ Tests/
│  └─ Companion.Api.Tests/
└─ web/                     git submodule (Next.js PWA); separate repo, separate agent
```

Dependency rules:

- `Companion.Events` references nothing. It contains records/classes only.
- `Companion.Core` references nothing internal.
- `Companion.Data` → `Core`.
- `Companion.Messaging` → `Core`, `Events`.
- Services → `Core`, `Data`, `Events`, `Messaging` as needed. **Services never reference each other.**
- No `src/` wrapper. Do not add one.

---

## 3. Service Responsibilities and Boundaries

| Service           | Owns                                                                                              | Consumes RabbitMQ | Publishes RabbitMQ    | Exposed to client |
| ----------------- | ------------------------------------------------------------------------------------------------- | ----------------- | --------------------- | ----------------- |
| **Api (gateway)** | Auth/RBAC, profiles, workspaces, expenses, billing, upload orchestration, report requests, outbox | **Never**         | Via outbox relay only | Yes (HTTP)        |
| **Extraction**    | OCR, validity checks, VAT classification, result columns                                          | Yes               | Result/status events  | No                |
| **Notification**  | Email (Resend), WhatsApp (planned)                                                                | Yes               | No                    | **No**            |
| **Realtime**      | SignalR hubs, backplane                                                                           | Yes               | No                    | Yes (WebSocket)   |
| **Reporting**     | Scheduled and ad-hoc reports (PDF/DOCX), store or email                                           | Yes               | Report-ready events   | No                |
| **Scheduler**     | Cron-style jobs, stuck-`Processing` sweeper                                                       | No                | Via events            | No                |

Hard rules:

- The gateway **never consumes** from RabbitMQ. It is the only client↔backend interface.
- The web client has **no direct access** to Notification, Extraction, Reporting or Scheduler.
- Realtime is a **speed-up, not the source of truth**. A page refresh must show current state from the DB via the gateway.
- Events carry **IDs and status only**. The client refetches data from the gateway.
- Billing logic lives in the gateway. **Authorization middleware must always verify the user is entitled to use the product** (active trial, Free, or Premium). No endpoint bypasses it.
- CORS: allow the web client origin only. No wildcard. Origins come from config.

---

## 4. Data Rules

**Database:** single shared PostgreSQL 17, single flat schema. No schema-per-service, no DB-per-service. Each service owns its own connection configuration.

**Migrations:** live in `Companion.Data`, applied on **gateway startup only**. Other services run a pending-migration check and fail fast if migrations are pending. Gateway `/health` must not report healthy until migrations complete. Other services wait for the gateway to be healthy.

**Keys and IDs:**

- All entity PKs are `int` identity (ASP.NET Core `IdentityUser<int>` pattern). Not UUID.
- Every API-exposed entity has a separate `PublicId` (`Guid`, non-PK, unique index).
- The int PK **never** appears in API responses, routes, JWT claims or client-visible events. Use `PublicId` everywhere outside the database.
- Int PKs are for internal FKs, joins and RBAC tables only.
- No Hashids / reversible encoding.

**Money:** `long AmountInCents` end to end (DB, DTOs, events). Convert to Rand only in the UI presentation layer. Never use `decimal`/`double` for stored or transmitted amounts. VAT amounts follow the same rule.

**Naming:** snake_case tables and columns (EFCore.NamingConventions). Plural table names. Timestamps are `timestamptz`, UTC only.

**Receipt status:** `ReceiptStatus` = `None | Uploaded | Processing | Qualified | Rejected`. Backend-canonical. The TRS v1.2 `proof_status` enum is superseded.

**Write ownership:**

- Extraction writes **only result columns**. User-origin data (manual entries, overrides) always wins.
- Extraction uses a **narrow DB role** with minimum privileges.
- Extraction persists results to the DB; the gateway reads them from there.

**Erasure:** POPIA erasure = anonymisation pipeline, not hard delete of financial records. Flag `is_anonymized`; strip personal identification fields.

---

## 5. Messaging Rules

- All RabbitMQ access goes through `Companion.Messaging` (EasyNetQ). No service references `EasyNetQ` or `RabbitMQ.Client` directly.
- **Use the existing generic publish/subscribe methods in `Companion.Messaging` for every RabbitMQ integration. Never create new messaging methods, helpers, wrappers or per-event publisher/consumer classes.** A new event needs only a model in `Companion.Events` plus a call to the existing generic method. If the generic methods cannot support a case, stop and report the gap; do not work around it or extend `Companion.Messaging` unprompted.
- EasyNetQ default conventions (type-based naming). No custom `IConventions`.
- `Companion.Events` holds models only. No methods with logic, no validation, no serialization attributes beyond what is required.
- **Transactional outbox** in the gateway: domain write + outbox row in one DB transaction; a relay publishes. Publishing failures never lose events and never fail user writes after commit.
- **Stuck-`Processing` sweeper** (Scheduler service): timeout 5 minutes, 3 retries, then mark failed.
- **Extraction consumer** must be idempotent, with a retry limit and a dead-letter queue.
- Propagate `X-Correlation-Id` across HTTP and RabbitMQ headers.
- Event naming: past tense, `<Thing><Happened>Event` (e.g. `ReceiptUploadedEvent`). Additive changes only; breaking changes need a new event type.
- **Known risk (R-4, outstanding):** auth email events currently carry OTP/reset secrets in plaintext. Until resolved: never log event payloads for these events, keep TTLs short, and do not extend this pattern to any other event. Do not "fix" this without an explicit prompt.

---

## 6. Receipt Pipeline (reference flow)

1. Client calls `POST /api/expenses/initialize` → gateway inserts expense (`ReceiptStatus.None`) and returns a short-lived presigned **Cloudflare R2** upload URL.
2. Client uploads the compressed WebP **directly to R2**.
3. Client calls `POST /api/capture/process` → gateway sets `Processing`, writes outbox row (`ReceiptUploadedEvent`: ids + status only).
4. Outbox relay publishes. Extraction consumes, runs OCR, validates tax-invoice markers, classifies VAT, writes result columns, publishes `ReceiptProcessedEvent`.
5. Realtime consumes and pushes a status nudge. Client refetches from the gateway.
6. Client also refetches on load/reconnect, with polling fallback. UI shows distinct **Processing / Extracted / Failed** states.

Free tier cap (20 expenses/month) is enforced at the **confirm/save** step, not at capture initiation.

---

## 7. Billing and Entitlements

- 30-day full-featured trial, then auto-downgrade to Free (not a cutoff).
- **Free:** 20 expenses/month, PDF export only, single-user workspace, reminders off.
- **Premium:** unlimited expenses, PDF + DOCX, up to 5 members (MVP), reminders on.
- PayFast with recurring billing token. Webhook handlers must validate signatures and be idempotent.
- Downgrade overage: expenses beyond the cap become read-only per item.
- Pricing (Free R0 / Premium R149) is hardcoded in the marketing page **deliberately and temporarily**. Do not revert to placeholders. Do not treat R149 as final.
- Entitlement checks go through one shared policy/service. No inline tier checks scattered in handlers.

---

## 8. Backend Coding Style (.NET 10 / C# 14)

**General**

- Nullable reference types enabled; warnings as errors in Release.
- File-scoped namespaces. One top-level type per file. File name = type name.
- Minimal APIs, grouped via `MapGroup` per feature, each group in its own `*Endpoints` static class. No controllers.
- Feature folders: `Features/<Feature>/` containing endpoints, request/response DTOs, validators, handler.
- Handlers are thin; business logic lives in services that are unit-testable without HTTP.
- `async` all the way. `CancellationToken` on every async public method, passed through.
- No `.Result`, `.Wait()`, `async void`.
- Prefer `record` for DTOs and events. Immutable by default.
- DI via constructor injection. No service locator. No static mutable state.
- Registration through extension methods: `services.AddCompanionPostgres(...)`, `services.AddCompanionMessaging(...)`, etc., living in `Companion.Core` / owning project.

**Runtime**

- Standard JIT-compiled .NET 10. **Native AOT is not used and not required.** Do not set `PublishAot`, do not add AOT/trimming analyzers, and do not constrain code or library choices for AOT compatibility (reflection, EF Core, QuestPDF, Open XML SDK are all fine).
- Do not reintroduce AOT without an explicit prompt.

**Validation and errors**

- Validate at the boundary (request DTOs). Return RFC 7807 `ProblemDetails`. No ad-hoc error shapes.
- Never return stack traces or internal IDs. Never leak whether an email exists on auth endpoints (login, forgot-password, resend OTP).
- Domain errors are explicit result types or typed exceptions mapped in one place. No exception-driven control flow for expected cases.

**Resilience (Polly)**

- Retry with jitter for transient failures; circuit breakers for external calls (Resend, PayFast, R2, OCR provider).
- Every external call has a timeout.
- Consistent error log schema across services (Section 10).

**Data access**

- Repositories live in `Companion.Data`. Handlers do not use `DbContext` directly unless the prompt says so.
- `AsNoTracking` for reads. Project to DTOs; never return entities from endpoints.
- Concurrency-sensitive flows (OTP issue/verify, login attempts) use DB transactions/locks.
- No raw SQL string concatenation. Parameterised only.
- Every migration is reviewed for data loss. No destructive migration without an explicit prompt.

**Naming**

- PascalCase types/methods, camelCase locals/params, `_camelCase` private fields, `I` prefix for interfaces, `Async` suffix on async methods.
- Configuration keys use `Section__Key` in env vars; options classes bound with `IOptions<T>` and validated on start (`ValidateOnStart`).

---

## 9. Testing Rules

Two layers per phase, both required:

1. **Integration:** xUnit + `WebApplicationFactory` against **Testcontainers PostgreSQL 17**. No in-memory EF provider for behaviour tests.
2. **Contract:** Schemathesis against the generated OpenAPI spec.

Additional:

- Swagger/OpenAPI is required on every HTTP service.
- Test names: `Method_Scenario_ExpectedResult`.
- Cover: happy path, validation failure, unauthorised, forbidden (wrong tenant/workspace), idempotency, concurrency where relevant.
- Tenant isolation test for every new workspace-scoped endpoint: user A cannot read or write user B's data.
- Tests must not depend on execution order or shared mutable state.
- A slice is **not done** while any test in the solution is red. If a pre-existing failure blocks you, report it with evidence; do not delete or skip tests to go green.
- Passing tests on empty scaffolds prove nothing. Test behaviour.

Current baseline (as of last audit): 12 of 71 tests passing, mostly outbox/publisher tests failing. Treat fixing the baseline as a prerequisite when a prompt says so.

---

## 10. Security, Config and Observability

**Secrets**

- No secrets in source, `appsettings.json`, or Docker images.
- `appsettings.Development.json` is gitignored.
- Local debug: `dotnet run` against localhost Postgres/RabbitMQ.
- Deployment: docker-compose with the `secrets/` folder pattern; production secrets from GCP Secret Manager.
- App must **fail on startup** if required secrets are missing (e.g. `Auth__Jwt__SigningKey`, `Auth__Otps__Pepper`). No fallback defaults for security material.

**Auth**

- JWT access + persisted refresh tokens with server-side revocation. Logout revokes.
- JWT claims use `PublicId`, never the int PK.
- Security-stamp check on every authenticated request.
- Email verification via OTP/magic link. OTPs are hashed with a pepper at rest and expire quickly.
- `RegisterRequest` requires POPIA consent; persist consent flag and timestamp.
- RBAC via permission codes through ASP.NET Core Identity (`platform-admin` role, `platform:*` claims). **No** `is_platform_admin` boolean.
- Rate limit auth endpoints (G2).

**Uploads**

- Presigned R2 URLs: short TTL, scoped to a single object key, content-type and size constrained. Object keys are server-generated and not guessable.

**Email**

- Resend is the only provider in all environments including local. No Mailpit, no Mailchimp.
- `INotificationChannel` abstraction: `EmailChannel` (wraps `IEmailProvider`/`ResendEmailProvider`), `WhatsAppChannel` (planned). Per-channel flags `Channels__Email__Enabled`, `Channels__WhatsApp__Enabled`.

**Logging (Serilog → self-hosted Seq)**

- Structured logs only. Message templates, not string interpolation.
- Every log carries `CorrelationId`, service name, environment.
- **Never log:** passwords, OTPs, reset tokens, JWTs, presigned URLs, full email bodies, receipt image content, full personal data. Redact or log `PublicId`.
- Frontend errors do **not** go to Seq directly and do **not** go through Notification. The route for client error reporting must be designed through the gateway; do not build one without a prompt.

**POPIA**

- Collect only what the feature needs. Track consent. Support erasure via anonymisation.
- Cross-border storage review (R2, Resend) is deferred past MVP. Do not make residency claims.

---

## 11. Frontend Rules (`web/`, separate repo and agent)

- Next.js (App Router, Server Components by default), TypeScript strict, ESLint clean, PWA, **375px baseline**, Lighthouse performance ≥ 90.
- Client components only where interaction requires them.
- `next/image` for gallery; `browser-image-compression` before upload; HTML5 media capture / `getUserMedia` for capture.
- `nuqs` for URL-synced filter state.
- `next-themes`, 2-state light/dark toggle (3-state explicitly deferred).
- Money arrives as cents; format to Rand only in presentation components via one shared formatter.
- The frontend consumes the backend contract. If a DTO/enum is missing or ambiguous, **ask**; do not invent.
- Mock mode: `NEXT_PUBLIC_MOCK_MODE`, optimistic auth by default, separate explicit toggle for simulated auth failure. Mock services implement the same interfaces as real ones so swapping is a config change.
- Never store tokens in `localStorage`. Follow the auth transport defined by the gateway contract.
- Captured photos must survive tab refresh (open item CP-07; use IndexedDB or equivalent when the prompt says so).
- Every screen implements loading, empty, error and offline states (Section 12).
- Accessibility: semantic HTML, labelled inputs, visible focus, 4.5:1 contrast, touch targets ≥ 44px, reduced-motion respected.
- Design direction: "trusted digital paper trail" / "Modern Ledger". Calm, credible, not playful. Follow the design theme document.

---

## 12. Mandatory Coverage Checklist

Every feature plan and every PR report must state, for each item, **Done / N/A (with reason) / Gap (flagged)**. A missing row is a defect.

| #   | Item               | Agent must address                                             |
| --- | ------------------ | -------------------------------------------------------------- |
| 1   | Onboarding         | First-run flow, workspace setup, VAT registration capture      |
| 2   | Sign up and log in | Validation, POPIA consent, token lifecycle                     |
| 3   | Email verification | OTP/magic link, expiry, resend throttling                      |
| 4   | Password reset     | Single-use expiring token, no account enumeration              |
| 5   | Account deletion   | POPIA erasure via anonymisation, token revocation              |
| 6   | User permissions   | RBAC permission codes, workspace roles, tenant isolation tests |
| 7   | Empty states       | Meaningful first-use UI with a clear next action               |
| 8   | Loading states     | Skeletons/spinners; never a blank screen                       |
| 9   | Error states       | Typed errors, retry affordance, no raw messages                |
| 10  | Network states     | Offline/slow/reconnect handling; queued or safe-fail uploads   |
| 11  | Data persistence   | No lost receipts; draft recovery; DB is source of truth        |
| 12  | Payment flow       | PayFast, trial → Free downgrade, webhook idempotency           |
| 13  | Notifications      | Event-driven via Notification service; channel flags           |
| 14  | Analytics          | Privacy-respecting events; no PII in payloads                  |
| 15  | Crash reporting    | Server via Seq; client path defined via gateway                |
| 16  | Privacy set up     | Consent, policy pages, data minimisation, erasure              |
| 17  | Accessibility      | Section 11 requirements                                        |
| 18  | Responsiveness     | 375px baseline up to desktop                                   |
| 19  | User flows         | End-to-end happy and failure paths documented and tested       |
| 20  | Beta testers       | Feature flags/invite path, feedback channel, test accounts     |

---

## 13. Superseded — Do Not Use

| Superseded                                                    | Replaced by                                                            |
| ------------------------------------------------------------- | ---------------------------------------------------------------------- |
| TRS v1.2 `docker-compose` section                             | Local compose: Postgres, RabbitMQ, Seq only                            |
| Database-per-service                                          | Single shared DB, flat schema                                          |
| UUID primary keys                                             | `int` PK + `PublicId` Guid                                             |
| Four-service split (Identity/Expense/Extraction/Notification) | Gateway API + Extraction, Notification, Realtime, Reporting, Scheduler |
| Mailpit, Mailchimp                                            | Resend everywhere                                                      |
| `proof_status` enum                                           | `ReceiptStatus`                                                        |
| `numeric(12,2)` money                                         | `long` cents                                                           |
| "No outbox for MVP, 503 on publish failure"                   | Transactional outbox + sweeper in MVP                                  |
| `SarsCompanion.Messaging` naming                              | `Companion.Messaging`                                                  |
| Native AOT (TRS "native AOT-compilation capabilities")        | Standard JIT runtime; AOT removed (2026-11-24)                         |
| Frontend errors via Notification service                      | Not defined yet; do not build                                          |

---

## 14. Open Items Agents Must Not Resolve Unprompted

- R-2: homepage "South African data storage" claim vs R2/Resend reality
- R-4: plaintext OTP/reset tokens in RabbitMQ events
- R-6: OCR engine not selected
- CP-07: captured photos lost on refresh
- RP-07: homepage CSV export vs "Free: PDF only"
- Final pricing
- Cross-border storage/POPIA review

Flag any new gap in your report instead of silently choosing a default.

---

## 15. Agent Report Template

Every session ends with:

1. **Scope delivered** (files changed, endpoints/events added)
2. **Verified** (commands run, test output, what was actually exercised)
3. **Not verified / stubbed**
4. **Test results** (counts, failures with cause)
5. **Checklist table** (Section 12)
6. **Decisions touched or conflicts found**
7. **Proposed next slice**
