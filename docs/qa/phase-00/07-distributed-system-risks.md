# Distributed System Reliability Assessment

## Reliability Mechanisms Implemented

| Mechanism | Implementation | Status |
|-----------|---------------|--------|
| Transactional Outbox | `OutboxMessage` table + `OutboxPublisher` background service | Implemented in API |
| Idempotency | Capture request ID with advisory lock (`pg_advisory_xact_lock`) | Implemented |
| Duplicate Message Handling | OTP single-use (consumed after verification), capture request ID dedup | Implemented |
| Message Acknowledgement | EasyNetQ publisher confirms, mandatory publish, persistent messages | Configured |
| Retry Policies | Exponential backoff (1s to 5min), max 10 attempts, then quarantine | Implemented |
| Dead-Letter Queue | Quarantined outbox rows (`failed_at IS NOT NULL`) for replay | Implemented |
| Message Ordering | Outbox published in ID order with `FOR UPDATE SKIP LOCKED` | Implemented |
| Timeout Handling | JWT clock skew = 0, OTP expiry configurable | Configured |
| Circuit Breakers | Not implemented (Polly referenced in Architecture.md but not in code) | Missing |
| Concurrency Control | Advisory locks for account mutations and capture requests | Implemented |
| Partial Transaction Failures | DB transactions with rollback on failure | Implemented |
| Graceful Shutdown | `IHostedService` with `CancellationToken` | Partial |
| Database Transaction Handling | `WithAccountTransactionAsync` with advisory lock serialization | Implemented |
| Eventual Consistency | Outbox relay pattern | Implemented |
| Service Recovery | Outbox retry with exponential backoff | Implemented |
| Backward Compatibility | Additive event changes only (Architecture.md rule) | Documented |

## Reliability Risks

| Risk | Description | Severity | Likelihood |
|------|-------------|----------|------------|
| No circuit breakers | External calls (Resend, PayFast, R2, OCR) have no circuit breaker protection | High | Medium |
| Workers idle | Extraction, Notification, Reporting, Scheduler workers have no active message consumers | High | High |
| No graceful shutdown for workers | Workers implement `BackgroundService` but no custom shutdown logic | Medium | Medium |
| Single shared database | All services share one PostgreSQL schema; schema changes affect all | Medium | High |
| Plaintext OTP in RabbitMQ | Auth events carry OTP codes in plaintext (known risk R-4) | High | Medium |
| No health checks for workers | Only API has `/health`; workers have no health endpoints | Medium | High |
| No dead-letter queue for consumers | Only outbox has quarantine; consumer DLQ not configured | Medium | Medium |
| No idempotency on consumers | Extraction consumer must be idempotent (Architecture.md rule) but not implemented | High | Medium |
| No message ordering guarantees across services | EasyNetQ default conventions; no explicit ordering | Low | Medium |
| No distributed tracing correlation | `CorrelationId` propagated but no trace ID integration | Low | Medium |

## Recommended Automated Tests (Future Phases)

| Test | Purpose | Phase |
|------|---------|-------|
| Circuit breaker test | Verify circuit opens after repeated failures | Phase 2 |
| Worker consumer test | Verify Extraction/Notification workers consume messages | Phase 2 |
| Graceful shutdown test | Verify workers handle SIGTERM/SIGINT | Phase 2 |
| Idempotent consumer test | Verify duplicate message handling | Phase 2 |
| Outbox replay test | Verify quarantined message replay | Phase 2 |
| Database failure test | Verify transaction rollback on DB outage | Phase 2 |
| Concurrent mutation test | Verify advisory lock serialization | Phase 2 |
| Token revocation test | Verify security stamp change invalidates JWT | Phase 2 |

## Evidence

- OutboxPublisher: `Services/Companion.Api/Services/OutboxPublisher.cs`
- OutboxPublisherTests: `Tests/Companion.Api.Tests/OutboxPublisherTests.cs`
- AuthenticationService: `Services/Companion.Api/Services/AuthenticationService.cs` (WithAccountTransactionAsync)
- Architecture.md Section 5: Messaging rules
- README.md: Outbox publisher relay description
