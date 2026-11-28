# Pilot Service Recommendation

## Candidate Evaluation

| Criteria | Companion.Api | Companion.Extraction | Companion.Notification | Companion.Realtime | Companion.Reporting | Companion.Scheduler |
|----------|--------------|---------------------|----------------------|-------------------|-------------------|-------------------|
| Meaningful business logic | Yes (auth, expenses, RBAC) | Yes (OCR, extraction) | Yes (email delivery) | No (no hubs) | No (idle) | No (idle) |
| Exposes API | Yes (REST) | No | No | Yes (WebSocket) | No | No |
| Uses database | Yes (full CRUD) | Yes (read/write) | Yes (profiles) | Yes (profiles) | Yes (read) | Yes (expenses) |
| RabbitMQ messages | Publishes (outbox) | Consumes + Publishes | Consumes | Consumes | Consumes + Publishes | Publishes |
| Identifiable dependencies | PostgreSQL, RabbitMQ | PostgreSQL, RabbitMQ | PostgreSQL, RabbitMQ | PostgreSQL, RabbitMQ | PostgreSQL, RabbitMQ | PostgreSQL, RabbitMQ |
| Representative of architecture | Yes (gateway pattern) | Yes (worker pattern) | Yes (worker pattern) | No (no hubs) | No (idle) | No (idle) |
| Implementation complexity | Medium | High | Medium | Low | Low | Low |
| Rollout risk | Medium | Medium | Medium | Low | Low | Low |
| Existing test coverage | High (integration) | Medium (unit) | Medium (unit) | None | None | None |

## Candidate Analysis

### Companion.Api (Gateway API) - RECOMMENDED

**Strengths:**
- Entry point for all client interactions
- Contains auth, RBAC, expenses, images, outbox
- Has comprehensive integration tests (WebApplicationFactory + Testcontainers)
- Exposes OpenAPI spec at `/openapi/v1.json`
- Publishes RabbitMQ messages via transactional outbox
- Has identifiable dependencies (PostgreSQL, RabbitMQ)
- Representative of the gateway pattern used across the architecture

**Weaknesses:**
- Medium complexity (auth + expenses + images + outbox)
- Medium rollout risk (gateway affects all clients)

**Rationale:** The gateway API is the natural pilot because it is the entry point for all client interactions, contains the most business logic, has the most comprehensive test coverage, and exercises all key infrastructure patterns (database, messaging, authentication, authorization). Improvements to QA here provide immediate value across the entire platform.

### Companion.Extraction

**Strengths:**
- Core business value (OCR, receipt processing)
- Uses database and RabbitMQ
- Has unit tests for parsers/classifiers

**Weaknesses:**
- Worker is idle (no active consumer)
- High complexity (OCR engine not selected)
- No API endpoint (worker only)

**Rationale:** Good candidate for Phase 2 after API QA is established, but not ideal for Phase 1 because the worker is idle and OCR engine is not selected (R-6).

### Companion.Notification

**Strengths:**
- Email delivery is critical for auth flows
- Uses RabbitMQ and database
- Has unit tests for templates

**Weaknesses:**
- Worker is idle (no active consumer)
- No API endpoint
- Depends on Resend integration (not configured)

**Rationale:** Good candidate for Phase 2, but email delivery is not yet functional end-to-end.

## Recommendation

**Select Companion.Api as the pilot service for Phase 1.**

Reasoning:
1. It is the gateway - all client traffic flows through it
2. It has the most existing test coverage (integration tests with real database)
3. It exercises all infrastructure patterns (DB, messaging, auth, authz)
4. QA improvements here provide immediate platform-wide value
5. It is representative of the patterns used in other services
6. Manageable complexity with limited rollout risk

## Phase 1 Scope for Pilot

- CI/CD pipeline for Companion.Api (build + test on PR)
- Code coverage measurement (Coverlet)
- Static code analysis (Roslyn analyzers)
- Secret scanning (Gitleaks)
- Dependency vulnerability scanning
- Contract testing (Schemathesis against OpenAPI)
- Database migration validation

## Evidence

- Service inventory: `docs/qa/phase-00/01-service-inventory.md`
- Architecture: `Architecture.md` Section 3
- Project files: `Services/Companion.Api/Companion.Api.csproj`
- Tests: `Tests/Companion.Api.Tests/`
