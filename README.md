# SARS Expense Companion

South African expense tracking PWA. This repository is the .NET solution
root: one gateway API, event-driven workers sharing a Postgres schema, and a
separate Next.js web client (`web/`, added as a git submodule).

## Stack

- .NET 10 / C# 14
- PostgreSQL 17
- RabbitMQ 4.0 (via EasyNetQ — added with the first feature that needs it)
- Serilog + self-hosted Seq
- EF Core (added with the first feature that needs it)

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

dotnet build SarsCompanion.slnx -c Release
dotnet test
dotnet run --project Services/Companion.Api      # http://localhost:5101/health
dotnet run --project Services/Companion.Realtime # http://localhost:5102/health
```

Local endpoints: Postgres `localhost:5432`, RabbitMQ management
`http://localhost:15672`, Seq `http://localhost:5341`.

## Notes

- `/health` on every web service is a stub; migration-gated health checks
  arrive in a later phase.
- Native AOT is deferred until a verified spike; no project publishes AOT.
