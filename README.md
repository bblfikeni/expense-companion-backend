# SARS Expense Companion

South African expense tracking PWA. This repository is the .NET solution
root: one gateway API, event-driven workers sharing a Postgres schema, and a
separate TanStack Start client in `web/`. The client uses this project as its backend
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
| `Services/Companion.Extraction/` | Receipt parsing components; worker not registered |
| `Services/Companion.Notification/` | Worker |
| `Services/Companion.Reporting/` | Worker |
| `Services/Companion.Scheduler/` | Worker |
| `Tests/Companion.Api.Tests/` | API, integration and extraction tests |
| `web/` | TanStack Start client (git submodule: `bblfikeni/biz-zen-guard`) |

Services never reference each other. Central package management is enabled in
`Directory.Packages.props`; solution-wide build settings live in
`Directory.Build.props`.

## Prerequisites

- .NET SDK 10
- Docker (for local infrastructure and integration tests)
- `dotnet-ef` tool (for EF Core migration commands)

## Getting started

```bash
cp .env.example .env        # then edit the credentials
docker compose up -d        # postgres, rabbitmq, seq

# Configure local API secrets in Services/Companion.Api/appsettings.Development.json.
# This file is ignored by git. Generate independent keys with:
openssl rand -base64 48
openssl rand -base64 48
# Use the first value for Auth:Jwt:SigningKey and the second for Auth:Otps:Pepper.
# Update ConnectionStrings:CompanionDb to match your PostgreSQL instance.
# Set RabbitMq:ConnectionString in both ignored API and Notification
# appsettings.Development.json files to the same RabbitMQ connection string.
# For real email delivery, set Resend__ApiKey and Resend__From (a verified sender).

dotnet build SarsCompanion.slnx -c Release
dotnet test
dotnet run --project Services/Companion.Api      # http://localhost:5101/health
dotnet run --project Services/Companion.Realtime # http://localhost:5102/health
DOTNET_ENVIRONMENT=Development dotnet run --project Services/Companion.Notification
```

Local endpoints: Postgres `localhost:5432`, RabbitMQ management
`http://localhost:15672`, Seq `http://localhost:5341`.

EF Core design-time commands read the connection string from the environment.
Set `ConnectionStrings__CompanionDb` to a connection string for your local
database (do not commit credentials), then run, for example:

```bash
dotnet ef migrations list --project Companion.Data
dotnet ef migrations has-pending-model-changes --project Companion.Data
dotnet ef database update --project Companion.Data
```

The test suite includes PostgreSQL integration tests using Testcontainers, so
Docker must be running when executing the full suite.

To use live authentication in `web/`, set `VITE_COMPANION_API_URL=http://localhost:5101`
in `web/.env.development.local` and restart Vite. `bun run dev:mobile` uses the HTTPS
development proxy. For direct HTTP development, set `Cors__AllowedOrigin` on the API
to the exact web origin. Leave the API URL unset to use the public demo. For Google, set
`VITE_GOOGLE_CLIENT_ID` to the same OAuth web client ID as `Auth__Google__ClientId`
and register the web origin as an authorised JavaScript origin. Registration and
password recovery require the Notification worker to deliver codes. Live sign-in
lasts until the access token expires or the page reloads. The workspace still
shows sample expense records. In live mode the receipt gallery shows the signed-in
user’s uploaded images from workspaces they can still access.

`GET /api/images` lists those uploads and requires `images:read`. It returns image
metadata with the expense ID and a `nextBefore` cursor. The default page contains
up to 12 images. Pass `before=<nextBefore>` for the next page. Fetch image bytes
with the same bearer token from `/api/expenses/{expenseId}/images/{public_id}/content`.

For local verification and reset codes, use a loopback RabbitMQ broker with the
dedicated `companion-dev-mail` virtual host. On a local Docker Compose broker, create
the virtual host and grant the local RabbitMQ user access.

```bash
docker compose exec rabbitmq rabbitmqctl add_vhost companion-dev-mail
docker compose exec rabbitmq rabbitmqctl set_permissions -p companion-dev-mail YOUR_RABBITMQ_USER '.*' '.*' '.*'
```

Set `virtualHost=companion-dev-mail` in `RabbitMq:ConnectionString` for the local API
and Notification worker, and restart the API. Use the same virtual host for any
other local workers that share this API's event stream. Run Notification in
Development with a private pickup directory under the Git-ignored `secrets/` folder.

```bash
DOTNET_ENVIRONMENT=Development Channels__Email__PickupDirectory="$PWD/secrets/dev-mail" \
  dotnet run --project Services/Companion.Notification
```

Open the resulting `.eml` file for your test address and copy the code into the
browser. The worker creates a private directory when needed and rejects existing
directories that grant access to other users. Use mode `0700` on Unix, with no
extended ACL on macOS, or restrict the Windows ACL to your account and system
administrators. Delete test mail when
finished. To use Resend, remove the pickup setting, move the API and worker to
their regular virtual host, and configure a verified sender and API key.

## Current implementation and remaining work

- The API supports registration, email verification, password and Google login, password recovery and
  profile updates. Profile endpoints require a bearer token and a matching
  `profilePublicId` query parameter. Password resets revoke existing bearer tokens.
- `POST /api/auth/verify-email` requires `email`, `code`, and `password`.
  The gateway sets the account password only when the email code is verified.
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
  The Notification worker delivers unexpired verification and reset codes through
  Resend. It sends the event ID as an idempotency key and completes only after
  provider acceptance. Persistent delivery failures go to EasyNetQ's error handling.
  Set `Channels__Email__Enabled=false` to disable email consumption explicitly.
  Configure `RabbitMq:ConnectionString` identically for the API and worker, using
  environment variables or their ignored `appsettings.Development.json` files.
  Outbox payloads contain plaintext delivery codes; access control and a retention
  policy are required for deployment.
- Extraction has receipt merging and field parsers for vendor, date, total, VAT,
  VAT number and tax-invoice wording. `FieldParser` composes them and returns
  per-field OCR confidence values. Confidence matching is text-based, so repeated
  tokens elsewhere in a receipt can affect a field's score.
- The extraction schema includes a unique `(expense_id, image_set_hash)` index,
  VAT number and six per-field confidence columns. The extraction host has no
  registered worker or receipt subscription; parsing is not an active pipeline.
  Reporting and Scheduler workers currently idle.
- Realtime has no SignalR hubs.
- Seq is available in Docker, but application logging currently goes to the console.
- Worker schema verification exists in the data library and is not wired into the
  placeholder workers. Wire it before adding jobs that access the database.

## Notes

- Image storage decision: the MVP stores receipt image metadata and bytes in
  separate PostgreSQL tables, not Cloudflare R2. This supersedes direct client
  uploads to R2 using presigned URLs; PostgreSQL Large Objects are not used.
  The frontend must upload images through the API and fetch content with its
  bearer token before rendering blob URLs. Extraction must read metadata and
  bytes from the database using a narrowly scoped read-only role and compute
  image set hashes from ordered image hashes. Monitor database and backup size.

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
