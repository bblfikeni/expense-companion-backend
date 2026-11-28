# Service Dependency Map

## Dependency Rules

- Services never reference each other.
- `Companion.Events` references nothing (models only).
- `Companion.Core` references nothing internal.
- `Companion.Data` → `Core`.
- `Companion.Messaging` → `Core`, `Events`.
- Services → `Core`, `Data`, `Events`, `Messaging` as needed.

## Service-to-Service HTTP Communication

None. Services do not communicate directly via HTTP. All inter-service communication goes through RabbitMQ or through the database (shared schema).

## RabbitMQ Dependencies

| Exchange/Topic | Publisher | Subscribers | Direction |
|----------------|-----------|-------------|-----------|
| EmailVerificationRequested | Companion.Api (outbox) | Companion.Notification | API → Worker |
| PasswordResetRequested | Companion.Api (outbox) | Companion.Notification | API → Worker |
| ReceiptUploadedEvent | Companion.Api (outbox) | Companion.Extraction, Companion.Realtime | API → Worker |
| ReceiptProcessedEvent | Companion.Extraction (planned) | Companion.Realtime, Companion.Reporting | Worker → Worker |

## Shared Library Dependencies

| Library | Depends On | Used By |
|---------|-----------|---------|
| Companion.Core | None | All libraries and services |
| Companion.Events | None | Companion.Messaging, all services |
| Companion.Configuration | None | All services |
| Companion.Messaging | Companion.Core, Companion.Events | Companion.Api, all workers |
| Companion.Data | Companion.Core | Companion.Api, all workers |
| Companion.Api | Companion.Core, Companion.Data, Companion.Events, Companion.Messaging | None (gateway) |

## Database Dependencies

| Service | Database Access | Schema Ownership | Migrations |
|---------|----------------|------------------|------------|
| Companion.Api | Read/Write | Gateway owns migrations | Applies on startup |
| Companion.Realtime | Read/Write | Shared schema | Verifies pending migrations |
| Companion.Extraction | Read/Write | Shared schema | Verifies pending migrations |
| Companion.Notification | Read/Write | Shared schema | Verifies pending migrations |
| Companion.Reporting | Read/Write | Shared schema | Verifies pending migrations |
| Companion.Scheduler | Read/Write | Shared schema | Verifies pending migrations |

**Risk:** Single shared database across all services creates coupling risk. Schema changes by the gateway affect all workers.

## External API Dependencies

| External Service | Used By | Purpose | Status |
|------------------|---------|---------|--------|
| Resend | Companion.Notification | Email delivery | Not yet integrated |
| PayFast | Companion.Api | Payment processing | Not yet integrated |
| Google OAuth | Companion.Api | Social login | Configured but not connected to real provider |
| Cloudflare R2 | Companion.Api | Image storage | MVP decision, not yet implemented |
| Seq | All services | Log aggregation | Provisioned, console logging only |

## Authentication Dependencies

| Dependency | Used By | Purpose |
|------------|---------|---------|
| PostgreSQL (profiles, roles tables) | Companion.Api | Identity storage |
| JWT signing key | Companion.Api | Token signing/validation |
| OTP pepper | Companion.Api | OTP hashing |

## Deployment Dependencies

| Service | Depends On | Notes |
|---------|-----------|-------|
| Companion.Api | PostgreSQL, RabbitMQ | Gateway applies migrations at startup |
| Companion.Realtime | PostgreSQL, RabbitMQ | Waits for gateway to be healthy |
| Companion.Extraction | PostgreSQL, RabbitMQ | Waits for gateway to be healthy |
| Companion.Notification | PostgreSQL, RabbitMQ | Waits for gateway to be healthy |
| Companion.Reporting | PostgreSQL, RabbitMQ | Waits for gateway to be healthy |
| Companion.Scheduler | PostgreSQL, RabbitMQ | Waits for gateway to be healthy |

## Risks

| Risk | Impact | Likelihood |
|------|--------|------------|
| Breaking API changes in Companion.Api affect all workers | High | Medium |
| Breaking message contract changes affect all subscribers | High | Medium |
| Shared database schema changes affect all services | High | Medium |
| Tight coupling through shared database | Medium | High |
| Circular dependencies: None detected | N/A | N/A |
| Missing contract versioning | Medium | High |
| Missing backward compatibility mechanisms | Medium | High |

## Evidence

- Architecture.md: Service responsibilities and messaging rules
- Companion.Data/CompanionDbContext.cs: Shared EF Core context
- Companion.Messaging/CompanionMessagingServiceCollectionExtensions.cs: RabbitMQ configuration
- Services/Companion.Api/Program.cs: Authorization policies and service registration
- infra/sql/extraction-role.sql: Database role for extraction service
