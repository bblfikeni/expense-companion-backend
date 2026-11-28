# Quality Baseline

## Measurable Baseline Metrics

| Metric | Value | Source |
|--------|-------|--------|
| Total microservices | 6 | Solution file, Services directory |
| Total shared libraries | 5 | Core, Configuration, Data, Events, Messaging |
| Total test projects | 1 | Tests/Companion.Api.Tests/ |
| Total automated tests | 248 | `dotnet test` output |
| Passing tests | 233 | `dotnet test` output |
| Failing tests | 15 | `dotnet test` output |
| Skipped tests | 0 | `dotnet test` output |
| Test execution duration | ~1m 43s | `dotnet test` output |
| Line and branch coverage | UNKNOWN | No coverage tooling configured |
| CI/CD pipeline duration | UNKNOWN | No CI/CD pipeline exists |
| Static analysis findings | UNKNOWN | No static analysis tooling |
| Security findings | UNKNOWN | No security scanning tooling |
| Critical workflows with automated coverage | 6 of 10 | Workflow assessment |
| Services with integration tests | 1 of 6 | Only Companion.Api has tests |
| Services with contract tests | 0 of 6 | No contract testing |
| Services with deployment health checks | 1 of 6 | Only API has `/health` |
| Known escaped defects | 15 failing tests | Test execution results |
| Known flaky tests | UNKNOWN | Not identified |

## Per-Service Test Coverage

| Service | Has Tests | Test Types | Notes |
|---------|-----------|------------|-------|
| Companion.Api | Yes | Integration (WebApplicationFactory + Testcontainers) | 248 tests in single project |
| Companion.Realtime | No | None | Worker idle, no SignalR hubs |
| Companion.Extraction | Partially | Unit tests for parsers/classifiers | Worker idle |
| Companion.Notification | Partially | Unit tests for templates | Worker idle |
| Companion.Reporting | No | None | Worker idle |
| Companion.Scheduler | No | None | Worker idle |

## Test Categories Baseline

| Category | Count | Status |
|----------|-------|--------|
| Unit tests | ~40 | Parser, field parser, classifier, validator tests |
| Integration tests | ~10 | AuthApiTests, ImageApiTests, ExpenseExtractionApiTests |
| Contract tests | 0 | No Schemathesis/Pact |
| End-to-end tests | 0 | No cross-service E2E |
| Performance tests | 0 | No k6/NBomber |
| Security tests | 0 | No DAST/SAST |
| Migration tests | Partial | ApplyCompanionMigrationsAsync in tests |
| Resilience tests | ~5 | OutboxPublisherTests with failure injection |
| Smoke tests | 0 | No automated smoke tests |

## Failing Tests Detail

| Test Class | Test | Failure |
|------------|------|---------|
| AuthApiTests | Jwt_ContainsPlatformAdminPermissionClaim | JWT missing `platform:*` claim |
| ImageApiTests | CaptureSubmission_RollsBackStatusWhenOutboxInsertFails | Forbidden instead of Created |
| ImageApiTests | EndpointsRequireTheirPermissionCodes | Permission check fails |
| ExpenseExtractionApiTests | ExtractionStatusIsWorkspaceScoped... | Forbidden instead of OK |
| 11 others | Various | Permission-related (policy change) |

## Test Infrastructure Baseline

| Component | Status | Notes |
|-----------|--------|-------|
| Test framework | xUnit 2.9.3 | Configured |
| Test runner | dotnet test | Configured |
| Integration test host | WebApplicationFactory | Configured |
| Test database | Testcontainers PostgreSQL 17 | Configured |
| Per-test database | Yes | Unique database per test |
| Mock messaging | RecordingBus | Used in OutboxPublisherTests |
| Real messaging | Testcontainers RabbitMQ | Used in MessagingTests |

## Evidence

- Test execution: `dotnet test --no-restore` → 233 passed, 15 failed, 0 skipped, ~1m 43s
- Test project: `Tests/Companion.Api.Tests/Companion.Api.Tests.csproj`
- Solution: `SarsCompanion.slnx`
