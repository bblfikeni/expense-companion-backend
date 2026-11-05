# SARS Expense Companion

South African expense tracking PWA. This repository is the .NET solution
root: one gateway API, event-driven workers sharing a Postgres schema, and a
separate Next.js client in `web/`. The client uses this project as its backend
through `Services/Companion.Api`.

## Stack

- .NET 10 / C# 14
- PostgreSQL 17
- RabbitMQ 4.0 (EasyNetQ)
- Serilog + self-hosted Seq
- EF Core 10 with Npgsql and ASP.NET Core Identity

## Layout

| Path | Purpose |
| --- | --- |
| `Companion.Core/` | Shared primitives (extensions, enums, DTOs). No dependencies. |
| `Companion.Configuration/` | Shared appsettings loading for all service hosts. |
| `Companion.Events/` | Shared event contracts in `Events/`. No dependencies. |
| `Companion.Messaging/` | Messaging plumbing over Core + Events. |
| `Companion.Data/` | Models, repositories, migrations over Core. |
| `Services/Companion.Api/` | Gateway API — `http://localhost:5101` |
| `Services/Companion.Realtime/` | SignalR host — `http://localhost:5102` |
| `Services/Companion.Extraction/` | Worker |
| `Services/Companion.Notification/` | Worker |
| `Services/Companion.Reporting/` | Worker |
| `Services/Companion.Scheduler/` | Worker |
| `Tests/Companion.Api.Tests/` | API smoke tests |
| `web/` | Next.js client (git submodule: `bblfikeni/biz-zen-guard`) |

Services never reference each other. Central package management is enabled in
`Directory.Packages.props`; solution-wide build settings live in
`Directory.Build.props`.

## Prerequisites

- .NET SDK 10
- Docker (for local infrastructure only)

## Getting started

```bash
cp .env.example .env        # then edit the credentials
docker compose up -d        # postgres, rabbitmq, seq

# Configure local API secrets in Services/Companion.Api/appsettings.Development.json.
# This file is ignored by git. Generate independent keys with:
openssl rand -base64 48
openssl rand -base64 48
# Use the first value for Auth:Jwt:SigningKey and the second for Auth:Otps:Pepper.
# Set Auth:Google:ClientId to your Google OAuth web client ID and update
# ConnectionStrings:CompanionDb if you changed the Postgres credentials.
# Set RabbitMq:ConnectionString in both ignored API and Notification
# appsettings.Development.json files to the same RabbitMQ connection string.

dotnet build SarsCompanion.slnx -c Release
dotnet test
dotnet run --project Services/Companion.Api      # http://localhost:5101/health
dotnet run --project Services/Companion.Realtime # http://localhost:5102/health
dotnet run --project Services/Companion.Notification
```

Local endpoints: Postgres `localhost:5432`, RabbitMQ management
`http://localhost:15672`, Seq `http://localhost:5341`.

## Current implementation and remaining work

- The API supports registration, email verification, password and Google login, password recovery and
  profile updates. Profile endpoints require a bearer token and a matching
  `profilePublicId` query parameter. Password resets revoke existing bearer tokens.
- Google sign-in accepts a Google ID token at `POST /api/auth/google`. Set
  `Auth__Google__ClientId` to the OAuth client ID used by the web client. First-time
  Google sign-up requires `popiaConsent: true` and an address for which Google is
  authoritative (Gmail, or verified Google Workspace with a hosted-domain claim).
  Returning users are identified by the linked Google subject. Email matches never
  automatically link existing password accounts: use password sign-in or recovery
  for those accounts. An authenticated account-linking flow is not implemented.
- Verification and recovery messages are written atomically to the outbox. The
  API's outbox publisher relays committed events through the shared
  `ICompanionMessageBus` implementation in `Companion.Messaging`. The Notification
  worker subscribes through that same EasyNetQ integration to the
  `EmailVerificationRequested` and
  `PasswordResetRequested` event types, then renders HTML and plain-text templates
  from `Services/Companion.Notification/Templates/`. Other services use
  `ICompanionMessageBus.PublishAsync<T>` and `SubscribeAsync<T>` for typed messages;
  both methods use the event type name as their topic. Delivery is at-least-once:
  messages may be published again if the process stops after publishing but before
  marking the outbox row. Publishing always requires persistent messages, broker
  confirmations and mandatory routing. Before a durable subscriber queue exists,
  unroutable messages stay in the outbox for retry.
  Transient failures use exponential backoff (1 second to 5 minutes); after 10
  attempts a row is quarantined with `failed_at`. Unsupported types and invalid
  JSON are quarantined immediately so later messages can proceed. Inspect retained
  rows with `failed_at IS NOT NULL`; after resolving the failure, explicitly replay
  an unexpired event by clearing `failed_at` and `next_attempt_at` and resetting
  `attempts` to zero. Request a fresh code if the original has expired.
  Email delivery is not implemented yet; Resend is planned. The Notification
  worker does not subscribe until an `INotificationSender` is registered. A sender
  must complete only after provider acceptance; handler failures are propagated
  to EasyNetQ's error handling rather than acknowledged as successful delivery.
  Configure `RabbitMq:ConnectionString` identically for the API and worker, using
  environment variables or their ignored `appsettings.Development.json` files.
  Outbox payloads contain plaintext delivery codes; access control and a retention
  policy are required for deployment.
- Extraction, Reporting and Scheduler workers currently idle.
- Realtime has no SignalR hubs.
- Seq is available in Docker, but application logging currently goes to the console.
- Worker schema verification exists in the data library and is not wired into the
  placeholder workers. Wire it before adding jobs that access the database.

## Notes

- Every service calls `builder.Configuration.AddCompanionAppSettings(builder.Environment)`
  from `Companion.Configuration`. It loads `appsettings.json` and optional
  `appsettings.{EnvironmentName}.json` from the service content root, reusing
  .NET host defaults without adding duplicate providers. Environment variables,
  command-line arguments and existing later overrides retain their precedence.
  Files added by the extension reload on change. The API currently validates and
  captures its auth and messaging options at startup, so restart after changing them.
  Set `DOTNET_ENVIRONMENT=Development` when running workers to load their local
  Development files. The API and Realtime launch profiles already select Development.

- The API applies migrations before startup and `/health` checks PostgreSQL
  connectivity. Realtime currently exposes a static liveness endpoint.
- Native AOT is deferred until a verified spike; no project publishes AOT.
