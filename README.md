# SARS Expense Companion

South African expense tracking PWA. This repository is the .NET solution
root: one gateway API, event-driven workers sharing a Postgres schema, and a
planned separate Next.js web client (`web/` has not been added yet).

## Stack

- .NET 10 / C# 14
- PostgreSQL 17
- RabbitMQ 4.0 (via EasyNetQ — added with the first feature that needs it)
- Serilog + self-hosted Seq
- EF Core 10 with Npgsql and ASP.NET Core Identity

## Layout

| Path | Purpose |
| --- | --- |
| `Companion.Core/` | Shared primitives (extensions, enums, DTOs). No dependencies. |
| `Companion.Events/` | Event contracts. No dependencies. |
| `Companion.Messaging/` | Messaging plumbing over Core + Events. |
| `Companion.Data/` | Models, repositories, migrations over Core. |
| `Services/Companion.Api/` | Gateway API — `http://localhost:5101` |
| `Services/Companion.Realtime/` | SignalR host — `http://localhost:5102` |
| `Services/Companion.Extraction/` | Worker |
| `Services/Companion.Notification/` | Worker |
| `Services/Companion.Reporting/` | Worker |
| `Services/Companion.Scheduler/` | Worker |
| `Tests/Companion.Api.Tests/` | API smoke tests |
| `web/` | Next.js client (git submodule — see `.gitmodules.TODO.md`) |

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

# Configure independent secrets before starting the API. These are read by .NET;
# docker compose's .env file is not automatically loaded by dotnet run.
export Auth__Jwt__SigningKey="$(openssl rand -base64 48)"
export Auth__Otps__Pepper="$(openssl rand -base64 48)"
# If you changed the Postgres credentials, also set ConnectionStrings__CompanionDb.

dotnet build SarsCompanion.slnx -c Release
dotnet test
dotnet run --project Services/Companion.Api      # http://localhost:5101/health
dotnet run --project Services/Companion.Realtime # http://localhost:5102/health
```

Local endpoints: Postgres `localhost:5432`, RabbitMQ management
`http://localhost:15672`, Seq `http://localhost:5341`.

## Current implementation and remaining work

- The API supports registration, email verification, login, password recovery and
  profile updates. Profile endpoints require a bearer token and a matching
  `profilePublicId` query parameter. Password resets revoke existing bearer tokens.
- Verification and recovery messages are written atomically to the outbox. **No
  relay or email provider is implemented yet**, so users cannot receive these
  messages through the application. Outbox payloads contain plaintext delivery
  codes; access control and a retention policy are required for deployment.
- Notification, Extraction, Reporting and Scheduler workers currently idle. The
  messaging library has no publisher or consumers; RabbitMQ is infrastructure only.
- Realtime has no SignalR hubs. The web client repository URL is still missing;
  see `.gitmodules.TODO.md`.
- Seq is available in Docker, but application logging currently goes to the console.
- Worker schema verification exists in the data library and is not wired into the
  placeholder workers. Wire it before adding jobs that access the database.

## Notes

- The API applies migrations before startup and `/health` checks PostgreSQL
  connectivity. Realtime currently exposes a static liveness endpoint.
- Native AOT is deferred until a verified spike; no project publishes AOT.
