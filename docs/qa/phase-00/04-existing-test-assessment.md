# Existing Test Assessment

## Test Project Summary

| Test Project | Framework | Runner | Location |
|--------------|-----------|--------|----------|
| Companion.Api.Tests | xUnit 2.9.3 | dotnet test | Tests/Companion.Api.Tests/ |

## Test Execution Results

| Metric | Value |
|--------|-------|
| Total tests | 248 |
| Passing | 233 |
| Failing | 15 |
| Skipped | 0 |
| Duration | ~1m 43s |
| Infrastructure required | Docker (Testcontainers PostgreSQL 17) |

## Test Files

## Test Categories

### Unit Tests
- **Present:** Yes (parser tests, field parser tests, classifier tests, etc.)
- **Framework:** xUnit
- **Mocks:** Yes (RecordingBus, RecordingMessageBus, stub handlers)
- **External dependencies:** None (pure unit tests)

### Integration Tests
- **Present:** Yes (AuthApiTests, ImageApiTests, ExpenseExtractionApiTests, ExpensesPermissionTests)
- **Framework:** xUnit + WebApplicationFactory + Testcontainers
- **External dependencies:** Docker (Testcontainers PostgreSQL 17)
- **Database:** Real PostgreSQL 17 container, per-test empty database

### Contract Tests
- **Present:** No
- **OpenAPI:** Available at `/openapi/v1.json` on the API
- **Schemathesis:** Not configured

### End-to-End Tests
- **Present:** No (no cross-service E2E tests)
- **Web client:** Separate repo (git submodule), not tested

### Performance Tests
- **Present:** No
- **Tools:** None configured

### Security Tests
- **Present:** No
- **Tools:** None configured

### Database Migration Tests
- **Present:** Partially (tests verify migrations apply via ApplyCompanionMigrationsAsync)
- **Tools:** EF Core migrations

### Resilience Tests
- **Present:** Partially (OutboxPublisherTests test retry and quarantine behavior)
- **Tools:** Custom RecordingBus with failure injection

### Smoke Tests
- **Present:** No
- **Health endpoint:** `/health` available but no automated smoke test

## Failing Tests (15)

| Test Class | Test Name | Failure Reason |
|------------|-----------|----------------|
| AuthApiTests | Jwt_ContainsPlatformAdminPermissionClaim | `AddToRoleAsync` succeeds but JWT doesn't contain `platform:*` claim |
| ImageApiTests | CaptureSubmission_RollsBackStatusWhenOutboxInsertFails | Returns Forbidden instead of Created (permissions) |
| ImageApiTests | EndpointsRequireTheirPermissionCodes | Permissions check fails with `platform:*` policy |
| ExpenseExtractionApiTests | ExtractionStatusIsWorkspaceScopedAndContainsOnlyPublicResultFields | Returns Forbidden instead of OK (permissions) |
| (11 others) | Various | Mostly permission-related failures after policy change |

**Note:** The 15 failing tests are largely related to the authorization policy change from `images:read`/`images:write` to `platform:*`. Tests that manually create JWT tokens with `images:read`/`images:write` claims now fail because the policy requires `platform:*`.

## Test Infrastructure

### CompanionApiFactory
- Extends `WebApplicationFactory<Program>`
- Uses Testcontainers PostgreSQL 17
- Configures real JWT Bearer authentication
- Disables OutboxPublisher by default
- Generates random signing key and pepper per test

### PostgresFixture
- Single PostgreSQL 17 container shared by all tests
- Creates a new empty database per test
- Uses Testcontainers.PostgreSql 4.16.0

### Test Collections
- `[Collection(PostgresCollection.Name)]` - Shared PostgreSQL container
- Tests within a collection share the fixture

## Quality and Effectiveness

| Aspect | Assessment |
|--------|------------|
| Assertion quality | High (specific status codes, database state verification, JWT claim extraction) |
| Test isolation | High (per-test database, Testcontainers) |
| Repeatability | High (deterministic databases, random emails) |
| Mocks vs real infrastructure | Mix (RecordingBus for messaging, real PostgreSQL for data) |
| External dependencies | Docker required for PostgreSQL container |

## Critical Business Logic Without Automated Tests

| Logic | Gap |
|-------|-----|
| Billling/PayFast integration | Not implemented |
| Account deletion/POPIA erasure | Not implemented |
| Workspace roles (owner/member) | Partial |
| Extraction OCR pipeline | Not implemented |
| Notification email delivery | Not implemented |
| Reporting/PDF generation | Not implemented |
| Scheduler cron jobs | Not implemented |

## Evidence

- Test results: `dotnet test --no-restore` → 233 passed, 15 failed, 0 skipped
- Test project: `Tests/Companion.Api.Tests/Companion.Api.Tests.csproj`
- Test infrastructure: `Tests/Companion.Api.Tests/Infrastructure/`
- Architecture.md Section 9: Testing rules