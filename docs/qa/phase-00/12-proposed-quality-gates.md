# Proposed Quality Gates

All thresholds are proposals until approved by stakeholders.

## Commit-Level Quality Gates

| Gate | Type | Threshold | Mandatory | Notes |
|------|------|-----------|-----------|-------|
| Build compilation | Mandatory | 0 errors | Yes | `dotnet build -c Release` |
| Code formatting | Advisory | `dotnet format --verify-no-changes` | No | Warn only |
| Secret scanning | Mandatory | 0 secrets detected | Yes | Gitleaks |
| Unit tests (changed code) | Mandatory | All pass | Yes | xUnit |

## Pull Request Quality Gates

| Gate | Type | Threshold | Mandatory | Notes |
|------|------|-----------|-----------|-------|
| Build compilation | Mandatory | 0 errors | Yes | `dotnet build -c Release` |
| Unit tests | Mandatory | All pass | Yes | Full test suite |
| Integration tests | Mandatory | All pass | Yes | Requires Docker |
| Code coverage (new code) | Mandatory | 80% line coverage | Yes | Coverlet |
| Code coverage (overall) | Advisory | 70% line coverage | No | Baseline tracking |
| Static analysis | Advisory | 0 new warnings | No | Roslyn analyzers |
| Secret scanning | Mandatory | 0 secrets | Yes | Gitleaks |
| Dependency scanning | Mandatory | 0 critical vulnerabilities | Yes | Trivy/Dependabot |
| Contract testing | Advisory | All contracts valid | No | Schemathesis |
| Branch protection | Mandatory | 1 required reviewer | Yes | GitHub/GitLab |

## Build-Level Quality Gates

| Gate | Type | Threshold | Mandatory | Notes |
|------|------|-----------|-----------|-------|
| Build compilation | Mandatory | 0 errors | Yes | All projects |
| Unit tests | Mandatory | All pass | Yes | Full suite |
| Integration tests | Mandatory | All pass | Yes | Requires Docker |
| Code coverage | Mandatory | 70% line coverage | Yes | Fail build below |
| Static analysis | Mandatory | 0 errors | Yes | Warnings as errors |
| Secret scanning | Mandatory | 0 secrets | Yes | Gitleaks |
| Dependency scanning | Mandatory | 0 critical | Yes | Trivy |
| Container scanning | Mandatory | 0 critical | Yes | Trivy (if images built) |

## Staging Deployment Quality Gates

| Gate | Type | Threshold | Mandatory | Notes |
|------|------|-----------|-----------|-------|
| Deployment health check | Mandatory | `/health` returns 200 | Yes | 30s timeout |
| Smoke tests | Mandatory | All pass | Yes | Critical endpoints |
| Database migration | Mandatory | All applied | Yes | No pending migrations |
| Rollback test | Advisory | Successful rollback | No | On failure only |

## Production Deployment Quality Gates

| Gate | Type | Threshold | Mandatory | Notes |
|------|------|-----------|-----------|-------|
| Staging validation | Mandatory | All staging gates pass | Yes | Promote from staging |
| Production health check | Mandatory | `/health` returns 200 | Yes | 60s timeout |
| Smoke tests | Mandatory | All pass | Yes | Critical endpoints |
| Database migration | Mandatory | All applied | Yes | Zero-downtime |
| Rollback plan | Mandatory | Documented and tested | Yes | RTO < 15 min |
| Post-deployment verification | Advisory | 5 min monitoring | No | Error rate < 1% |

## Post-Deployment Monitoring Quality Gates

| Gate | Type | Threshold | Mandatory | Notes |
|------|------|-----------|-----------|-------|
| Error rate | Advisory | < 1% | No | 5-minute window |
| Response time (p95) | Advisory | < 500ms | No | API endpoint |
| Database connections | Advisory | < 80% pool | No | PostgreSQL |
| Message queue depth | Advisory | < 100 messages | No | RabbitMQ |

## Quality Threshold Application Policy

### New and Modified Code First
For existing technical debt, enforce quality thresholds on new and modified code first:
- Code coverage: 80% on new/changed files only
- Static analysis: 0 new warnings on new/changed code
- Unit tests: All new code must have tests

### Legacy Code
- No enforcement on existing code until refactored
- Gradual improvement through boy-scout rule (leave code better than found)

## Exception Policy

| Exception Type | Approval Authority | Duration | Conditions |
|---------------|-------------------|----------|------------|
| Coverage threshold | Tech Lead | 1 sprint | Documented justification |
| Static analysis warning | Tech Lead | Permanent | Documented false positive |
| Dependency vulnerability | Security Lead | 7 days | Mitigation plan required |
| Secret scanning | CISO | Never | No exceptions |

## Quality Gate Ownership

| Role | Responsibility |
|------|---------------|
| Tech Lead | Gate definition, threshold approval, exception review |
| Security Lead | Security gate approval, vulnerability assessment |
| DevOps Engineer | CI/CD pipeline implementation, gate enforcement |
| Developers | Gate compliance, test writing, remediation |

## Approval Responsibilities

| Decision | Approver |
|----------|----------|
| Quality gate thresholds | Tech Lead + Security Lead |
| Exception requests | Tech Lead (coverage/static) / Security Lead (security) |
| Threshold changes | Tech Lead + Team consensus |
| Gate removal/modification | Tech Lead + Product Owner |

## Evidence

- CI/CD assessment: `docs/qa/phase-00/05-cicd-assessment.md`
- Quality gap analysis: `docs/qa/phase-00/09-quality-gap-analysis.md`
