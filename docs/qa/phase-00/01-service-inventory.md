# Service Inventory

## Microservices

| Service | Type | Port | Responsibility | Database | RabbitMQ | Exposed To Client |
|---------|------|------|---------------|----------|----------|-------------------|
| Companion.Api | Web API | 5101/8080 | Gateway: auth/RBAC, profiles, workspaces, expenses, billing, upload orchestration, report requests, outbox | Shared Postgres | Publishes only (via outbox relay) | Yes (HTTP) |
| Companion.Realtime | Web API | 5102 | SignalR push hubs, backplane | Shared Postgres | Consumes | Yes (WebSocket) |
| Companion.Extraction | Worker | N/A | OCR, receipt processing, VAT classification, result columns | Shared Postgres | Consumes + Publishes | No |
| Companion.Notification | Worker | N/A | Email (Resend), WhatsApp (planned) | Shared Postgres | Consumes | No |
| Companion.Reporting | Worker | N/A | Scheduled + ad-hoc reports (PDF/DOCX) | Shared Postgres | Consumes + Publishes | No |
| Companion.Scheduler | Worker | N/A | Cron-style jobs, stuck-Processing sweeper | Shared Postgres | Publishes via events | No |

## Shared Libraries

| Library | Type | Dependencies | Purpose |
|---------|------|-------------|---------|
| Companion.Core | Class Library | None | Shared primitives, extensions, enums, DTOs |
| Companion.Configuration | Class Library | None | Shared appsettings loading for all service hosts |
| Companion.Data | Class Library | Companion.Core | EF Core models, repositories, migrations |
| Companion.Events | Class Library | None | Event models only, no logic |
| Companion.Messaging | Class Library | Companion.Core, Companion.Events | EasyNetQ connection strategies, generic pub/sub helpers |

## Solution Structure

```
Companion/
├─ Companion.Core/          shared extensions, startup extensions, enums, DTOs
├─ Companion.Data/          EF Core (code-first, PostgreSQL), models, repositories, migrations
├─ Companion.Events/        event models ONLY. No logic.
├─ Companion.Messaging/     EasyNetQ connection strategies, generic pub/sub helpers
├─ Companion.Configuration/ shared appsettings loading
├─ Services/
│  ├─ Companion.Api/            gateway: identity + expenses + billing + upload orchestration
│  ├─ Companion.Extraction/     OCR / receipt processing (RabbitMQ consumer)
│  ├─ Companion.Notification/   email / WhatsApp (RabbitMQ consumer)
│  ├─ Companion.Realtime/       SignalR push (RabbitMQ consumer, backplane)
│  ├─ Companion.Reporting/      scheduled + ad-hoc report generation (RabbitMQ consumer)
│  └─ Companion.Scheduler/      all scheduled jobs incl. stuck-Processing sweeper
├─ Tests/
│  └─ Companion.Api.Tests/
└─ web/                     git submodule (Next.js PWA); separate repo
```

## Dependency Rules (from Architecture.md)

- `Companion.Events` references nothing. Contains records/classes only.
- `Companion.Core` references nothing internal.
- `Companion.Data` → `Core`.
- `Companion.Messaging` → `Core`, `Events`.
- Services → `Core`, `Data`, `Events`, `Messaging` as needed. **Services never reference each other.**

## Framework Versions

| Component | Version | Source |
|-----------|---------|--------|
| .NET SDK | 10.0 | Directory.Build.props |
| C# | 14 (default for net10.0) | Directory.Build.props |
| ASP.NET Core | 10.0.13 | Directory.Packages.props |
| EF Core | 10.0.13 | Directory.Packages.props |
| Npgsql | 10.0.3 | Directory.Packages.props |
| ASP.NET Core Identity | 10.0.13 | Directory.Packages.props |
| EasyNetQ | 8.1.11 | Directory.Packages.props |
| Serilog | 10.0.0 | Directory.Packages.props |
| xUnit | 2.9.3 | Directory.Packages.props |
| Testcontainers.PostgreSql | 4.16.0 | Directory.Packages.props |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | Directory.Packages.props |
| PostgreSQL | 17-alpine | docker-compose.yml |
| RabbitMQ | 4.0-management-alpine | docker-compose.yml |

## External Dependencies

| Dependency | Purpose | Configuration |
|------------|---------|---------------|
| PostgreSQL 17 | Primary database | `ConnectionStrings:CompanionDb` |
| RabbitMQ 4.0 | Message broker | `RabbitMq:ConnectionString` |
| Seq | Log aggregation | Docker service (not wired to app yet) |
| Resend | Email delivery | `Resend:ApiKey`, `Resend:From` (planned) |
| PayFast | Payment processing | Not yet integrated |
| Cloudflare R2 | Image storage | MVP decision; not yet implemented |
| Google OAuth | Social login | `Auth:Google:ClientId` |

## Evidence

- Solution file: `SarsCompanion.slnx`
- Project references: All `.csproj` files under `Services/` and shared libraries
- Package versions: `Directory.Packages.props`
- Build settings: `Directory.Build.props`
- Docker compose: `docker-compose.yml`
- Service compose files: `Services/Companion.Api/compose.yml`, `Services/Companion.Extraction/compose.yml`, `web/compose.yml`
- README.md: Service layout and responsibilities
