# Phase 01 Readiness

## Phase 0 Completion Checklist

| Item | Status | Evidence |
|------|--------|----------|
| All microservices inventoried | Complete | `01-service-inventory.md` |
| Architecture documented | Complete | `02-as-built-architecture.md` |
| Service dependencies mapped | Complete | `03-service-dependency-map.md` |
| Testing capabilities assessed | Complete | `04-existing-test-assessment.md` |
| CI/CD quality gates assessed | Complete | `05-cicd-assessment.md` |
| Critical business workflows identified | Complete | `06-critical-business-workflows.md` |
| Distributed system risks documented | Complete | `07-distributed-system-risks.md` |
| Infrastructure readiness assessed | Complete | `08-infrastructure-readiness.md` |
| Quality gap analysis | Complete | `09-quality-gap-analysis.md` |
| Quality baseline metrics recorded | Complete | `10-quality-baseline.md` |
| Pilot microservice recommended | Complete | `11-pilot-service-recommendation.md` |
| Proposed quality gates documented | Complete | `12-proposed-quality-gates.md` |
| Decision log | Complete | `13-decision-log.md` |
| Phase 01 readiness | Complete | `14-phase-01-readiness.md` |
| Phase 0 report | Complete | `docs/qa/phase-00-report.md` |

## Phase 1 Prerequisites

### Infrastructure Prerequisites

| Prerequisite | Status | Notes |
|--------------|--------|-------|
| Docker available | Available | docker-compose.yml exists |
| .NET SDK 10 | Available | Required for build/test |
| GitHub repository access | UNKNOWN | CI/CD requires repository access |
| Container registry | UNKNOWN | Needed for container scanning |

### Tooling Prerequisites

| Tool | Status | Installation |
|------|--------|--------------|
| xUnit | Installed | Already in test project |
| Testcontainers | Installed | Already used for PostgreSQL |
| Coverlet | Not installed | Need to add NuGet package |
| Gitleaks | Not installed | Need CI integration |
| Trivy | Not installed | Need CI integration |
| SonarQube | Not installed | Requires server |

### Process Prerequisites

| Process | Status | Notes |
|---------|--------|-------|
| Branch protection | Not configured | Requires repository admin |
| PR review workflow | Not configured | Requires team agreement |
| Quality gate approval | Not defined | Requires stakeholder sign-off |

## Phase 1 Recommended Scope

### Week 1: CI/CD Foundation
1. Create GitHub Actions workflow for build + test on PR
2. Add Docker setup for CI (PostgreSQL via Testcontainers or service container)
3. Configure test result reporting

### Week 2: Quality Measurement
1. Add Coverlet for code coverage
2. Configure coverage thresholds (70% overall, 80% new code)
3. Add secret scanning (Gitleaks)

### Week 3: Security and Reliability
1. Add dependency vulnerability scanning (Trivy/Dependabot)
2. Add container vulnerability scanning (Trivy)
3. Add static code analysis (Roslyn analyzers)

### Week 4: Contract and Migration
1. Add contract testing (Schemathesis against OpenAPI)
2. Add database migration validation
3. Configure branch protection rules

## Success Criteria for Phase 1

| Criterion | Target | Measurement |
|-----------|--------|-------------|
| CI pipeline runs on every PR | 100% | GitHub Actions status |
| Build passes on main | 100% | CI status |
| All tests pass (excluding pre-existing failures) | 100% | `dotnet test` |
| Code coverage measured | > 70% | Coverlet report |
| No secrets in commits | 0 | Gitleaks scan |
| No critical vulnerabilities | 0 | Trivy scan |

## Risks and Blockers

| Risk | Impact | Mitigation |
|------|--------|------------|
| No CI/CD pipeline exists | High | Start with simple GitHub Actions workflow |
| 15 pre-existing failing tests | Medium | Document as baseline; exclude from gate initially |
| No repository admin access | High | Coordinate with repo owner for branch protection |
| Docker not available in CI | Medium | Use service containers or self-hosted runner |

## Evidence

- All Phase 0 documents in `docs/qa/phase-00/`
- Test results: `dotnet test --no-restore` → 233 passed, 15 failed, 0 skipped
