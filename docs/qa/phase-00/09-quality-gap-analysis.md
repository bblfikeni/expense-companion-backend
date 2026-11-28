# Quality Gap Analysis

## Quality Gap Matrix

| Capability | Current State | Target State | Gap | Risk | Priority | Recommended Phase |
|------------|--------------|--------------|-----|------|----------|-------------------|
| CI/CD pipeline | Not implemented | GitHub Actions with build+test on PR | No automated build/test on commit | High | Critical | Phase 1 |
| Code formatting | Not implemented | `dotnet format` enforced | No formatting enforcement | Low | Medium | Phase 1 |
| Static code analysis | Not implemented | SonarQube or Roslyn analyzers | No code quality enforcement | Medium | High | Phase 2 |
| Unit tests | Implemented (parser/classifier tests) | Maintain and expand | Adequate for unit level | Low | Low | Phase 1 |
| Integration tests | Implemented (WebApplicationFactory + Testcontainers) | Maintain and expand | Adequate for API level | Low | Low | Phase 1 |
| Code coverage | Not implemented | Coverlet with thresholds | No coverage measurement | High | High | Phase 1 |
| Secret scanning | Not implemented | Gitleaks | No secret detection | Medium | Medium | Phase 1 |
| Dependency vulnerability scanning | Not implemented | Trivy/Dependabot | Supply chain risk | Medium | Medium | Phase 1 |
| Container vulnerability scanning | Not implemented | Trivy | Container image risk | Medium | Medium | Phase 2 |
| Contract testing (Schemathesis/Pact) | Not implemented | Contract validation | Service compatibility unvalidated | Medium | Medium | Phase 2 |
| Database migration validation | Not implemented | Migration safety checks | Breaking schema changes | Medium | Medium | Phase 2 |
| Kubernetes manifest validation | Not applicable | N/A | No K8s manifests | N/A | N/A | N/A |
| Pull request approvals | Not implemented | Branch protection | No PR governance | High | High | Phase 1 |
| Branch protection | Not implemented | Required reviews/status checks | No merge governance | High | High | Phase 1 |
| Deployment health checks | Partially (`/health` exists) | Automated health verification | No automated deployment checks | Medium | Medium | Phase 2 |
| Automated rollback | Not implemented | Rollback capability | No recovery from bad deployments | High | Medium | Phase 2 |
| Performance testing | Not implemented | k6/NBomber | No performance baselines | Low | Low | Phase 3 |
| Security testing | Not implemented | DAST/SAST | No security validation | High | Medium | Phase 2 |
| Chaos engineering | Not implemented | Failure injection | No resilience validation | Low | Low | Phase 3 |

## Detailed Gap Descriptions

### CI/CD Pipeline (Critical)
**Current:** No CI/CD configuration exists. No GitHub Actions, GitLab CI, or similar.
**Target:** GitHub Actions workflow that builds, tests, and reports on every PR and push to main.
**Evidence:** Repository root has no `.github/`, `.gitlab-ci.yml`, `Jenkinsfile`, or similar.

### Code Coverage (High)
**Current:** No coverage measurement configured.
**Target:** Coverlet integrated with thresholds (e.g., 80% line coverage).
**Evidence:** `Directory.Packages.props` has no Coverlet reference; test project has no coverage configuration.

### Static Code Analysis (High)
**Current:** Only compiler warnings (treated as errors).
**Target:** SonarQube or Roslyn analyzers for code quality rules.
**Evidence:** `Directory.Build.props` has `TreatWarningsAsErrors=true` but no additional analyzers.

### Secret Scanning (Medium)
**Current:** No secret scanning configured.
**Target:** Gitleaks in CI to prevent credential commits.
**Evidence:** No Gitleaks configuration found; `.env.example` exists but `.env` is gitignored.

### Dependency Scanning (Medium)
**Current:** No vulnerability scanning for NuGet packages.
**Target:** Trivy or Dependabot for dependency vulnerabilities.
**Evidence:** `Directory.Packages.props` lists package versions but no scanning configured.

### Contract Testing (Medium)
**Current:** OpenAPI spec available at `/openapi/v1.json` but no contract tests.
**Target:** Schemathesis or PactNet for API contract validation.
**Evidence:** ImageApiTests has `OpenApiAndCors_ExposeImageContract` test that checks OpenAPI content, but no automated contract testing tool.

### Database Migration Validation (Medium)
**Current:** Migrations applied on gateway startup; workers check for pending migrations.
**Target:** Automated migration validation in CI (dry-run, compatibility check).
**Evidence:** `Companion.Data/Migrations/` has 9 migrations; `ApplyCompanionMigrationsAsync` called in `Program.cs:67`.

### Pull Request Approvals (High)
**Current:** No branch protection or PR governance.
**Target:** Required reviews and status checks before merge.
**Evidence:** No GitHub/GitLab branch protection configuration found.

### Automated Rollback (Medium)
**Current:** No deployment automation exists.
**Target:** Automated rollback on deployment failure.
**Evidence:** No CI/CD pipeline, no deployment automation.

## Evidence

- CI/CD assessment: `docs/qa/phase-00/05-cicd-assessment.md`
- Test assessment: `docs/qa/phase-00/04-existing-test-assessment.md`
- Project files: `Directory.Build.props`, `Directory.Packages.props`
- Solution: `SarsCompanion.slnx`
