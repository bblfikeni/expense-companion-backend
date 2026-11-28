# Infrastructure and Tooling Readiness

## Tooling Readiness Assessment

| Tool | Purpose | Available | Can Be Used | Notes |
|------|---------|-----------|-------------|-------|
| xUnit | Test framework | Yes | Yes | Already used |
| Coverlet | Code coverage | No | Yes | Not installed |
| Testcontainers | Integration test containers | Yes | Yes | Already used (PostgreSQL) |
| WireMock.NET | HTTP mocking | No | Yes | Not installed |
| SonarQube | Static analysis | No | No | Not installed, requires server |
| ArchUnitNET | Architecture testing | No | Yes | Not installed |
| PactNet / Pact Broker | Contract testing | No | Yes | Not installed |
| Stryker.NET | Mutation testing | No | Yes | Not installed |
| Playwright | E2E browser testing | No | Yes | web/node_modules has playwright |
| Gitleaks | Secret scanning | No | Yes | Not installed |
| Trivy | Container scanning | No | Yes | Not installed |
| k6 / NBomber | Performance testing | No | Yes | Not installed |
| Kubernetes verification | K8s deployment testing | No | N/A | No K8s manifests |

## CI Runner Capacity

| Resource | Status | Notes |
|----------|--------|-------|
| Docker | Available | docker-compose.yml exists |
| .NET SDK 10 | Available | Required for build/test |
| PostgreSQL 17 | Available | Via Testcontainers or docker-compose |
| RabbitMQ 4.0 | Available | Via docker-compose |
| Kubernetes | Not available | No K8s manifests found |
| Cloud provider | Not configured | GCP Secret Manager mentioned in Architecture.md |

## Test Execution Isolation

| Aspect | Status |
|--------|--------|
| Per-test database | Yes (Testcontainers, unique database per test) |
| Shared fixture | Yes (PostgresFixture shared across collection) |
| Parallel test safety | Limited (shared container, per-test databases) |
| Deterministic data | Yes (random emails, GUIDs) |

## Infrastructure Resource Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| CPU | 2 cores | 4 cores |
| Memory | 4 GB | 8 GB |
| Disk | 20 GB | 50 GB |
| Docker | Required | Required |
| PostgreSQL | 17 | 17 |
| RabbitMQ | 4.0 | 4.0 |

## Existing Tooling That Can Be Reused

| Tool | Current Use | Can Be Extended |
|------|-------------|-----------------|
| xUnit | Test framework | Yes |
| Testcontainers | PostgreSQL integration tests | Yes (RabbitMQ container also used in MessagingTests) |
| WebApplicationFactory | API integration tests | Yes |
| EasyNetQ | Messaging | Yes (RecordingBus for tests) |
| Serilog | Logging | Yes (console only, Seq available) |
| EF Core Migrations | Database schema | Yes |
| Health Checks | `/health` endpoint | Yes (PostgreSQL check registered) |

## Licensing and Cost

| Tool | License | Cost |
|------|---------|------|
| xUnit | MIT | Free |
| Testcontainers | Apache 2.0 | Free |
| Coverlet | MIT | Free |
| SonarQube | LGPL (community) | Free (self-hosted) |
| ArchUnitNET | MIT | Free |
| PactNet | MIT | Free |
| Stryker.NET | MIT | Free |
| Playwright | MIT | Free |
| Gitleaks | MIT | Free |
| Trivy | Apache 2.0 | Free |

All recommended tools are open-source and cost-effective.

## Evidence

- Test project: `Tests/Companion.Api.Tests/Companion.Api.Tests.csproj` (xUnit, Testcontainers)
- MessagingTests: Uses Testcontainers RabbitMQ container
- Docker files: `docker-compose.yml`, service compose files
- Architecture.md Section 10: Security, Config and Observability
