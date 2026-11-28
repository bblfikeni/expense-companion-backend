# Decision Log

## Decision 1: Pilot Service Selection

**Decision:** Select Companion.Api as the pilot microservice for Phase 1.

**Rationale:**
- Gateway API is the entry point for all client interactions
- Contains the most business logic (auth, RBAC, expenses, images, outbox)
- Has comprehensive integration tests (WebApplicationFactory + Testcontainers)
- Exercises all key infrastructure patterns (DB, messaging, auth, authorization)
- Representative of the gateway pattern used across the architecture
- Manageable complexity with limited rollout risk

**Date:** Phase 0 assessment

**Alternatives Considered:**
- Companion.Extraction: Good candidate but worker is idle, OCR engine not selected
- Companion.Notification: Worker idle, no API endpoint
- Companion.Realtime/Reporting/Scheduler: Workers idle, no meaningful logic

**Evidence:** `docs/qa/phase-00/11-pilot-service-recommendation.md`

## Decision 2: Tool Selection Preference

**Decision:** Prefer open-source, cost-effective tools that can be self-hosted.

**Rationale:**
- All recommended tools (xUnit, Testcontainers, Coverlet, Gitleaks, Trivy, etc.) are MIT/Apache licensed
- No licensing costs
- Can be self-hosted where applicable (SonarQube, Pact Broker)
- Aligns with existing .NET ecosystem

**Evidence:** `docs/qa/phase-00/08-infrastructure-readiness.md`

## Decision 3: Quality Gate Enforcement Strategy

**Decision:** Enforce quality thresholds on new and modified code first, with gradual improvement for legacy code.

**Rationale:**
- Existing technical debt (15 failing tests) should not block all progress
- Boy-scout rule encourages incremental improvement
- New code must meet higher standards immediately

**Evidence:** `docs/qa/phase-00/12-proposed-quality-gates.md`

## Decision 4: Documentation-First Approach

**Decision:** Phase 0 is documentation-only; no application code or infrastructure changes.

**Rationale:**
- Compliance with Phase 0 restrictions
- Establish baseline before implementing changes
- All findings supported by repository evidence

**Evidence:** All documentation files in `docs/qa/phase-00/`
