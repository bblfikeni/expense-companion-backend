# SARS Expense Companion - Analysis Summary

**Based on:** Product-Requirements.md v1.0 (2026-11-23) vs Codebase analysis  
**Total Discrepancies:** 18 major items across 8 requirement categories  
**Implementation Status:** Phase 1 foundation skeleton (2026-10-18)  
**Test Status:** 12 of 71 tests passing (59 failed)

---
## Critical High-Severity Discrepancies

| ID | Issue | Impact |
|----|-------|--------|
| R-2 | False "South African data storage" claim | Legal/liability risk |
| CP-07 | Photos lost on tab close/refresh | Data loss prevention failure |
| RP-07 | CSV export contradicts RP-05 | Feature consistency issue |
| R-4 | Plaintext tokens in RabbitMQ events | Security vulnerability |
| R-6 | OCR engine not selected | Core product risk |
## Medium-Severity Discrepancies

| ID | Issue | Impact |
|----|-------|--------|
| ID-10 | Account deletion/POPIA erasure | POPIA compliance gap |
| ID-11 | Workspace roles (owner/member) | Premium feature gap |
| NT-03 | Notification channel abstraction | Feature completeness |
| BL-02 | PayFast billing integration | Revenue critical path |
| R-7 | Duplicate detection logic | Data integrity risk |
## Low-Severity Discrepancies

| ID | Issue | Impact |
|----|-------|--------|
| R-1 | AOT compatibility | Production risk |
| R-5 | Pricing R149 unvalidated | Not final for launch |
| R-8 | Account deletion unspecified | Compliance risk |
| R-11 | Capture edge case | User experience |
| R-8 | Account deletion unspecified | Compliance risk |
## Implementation Status by Category

| Category | Implemented | Gaps | Priority |
|----------|-------------|------|----------|
| Auth | ~40% | Email verification no delivery, password reset no delivery, RBAC partial | High |
| Capture | ~25% | CP-07: photos lost on tab; CP-04: R2 flow partial | High |
| Extraction | ~10% | Worker idle; no OCR pipeline; EX-07 open; EX-08 not running | High |
| Reporting | ~15% | RP-05/RP-07 conflict; samples only | Medium |
| Notifications | ~25% | Templates built; channel abstraction missing; plaintext tokens risk | Medium |
## Standing Checklist Gaps (Top 5)

| # | Item | Gap |
|---|------|-----|
| 1 | Onboarding | No first-run flow: workspace setup, business type, VAT status, first-capture prompt |
| 3 | Email verification | No outbox relay or email delivery → non-functional end to end |
| 4 | Password reset | Same delivery gap as email verification |
| 11 | Data persistence | Mock capture holds photos in tab memory; no draft recovery after tab close |
| 12 | Payment flow | Nothing built; pricing not final; failed-payment/dunning undefined |
## Critical Risks (from §14 of Product Requirements)

| ID | Item | Severity | Action |
|----|------|----------|--------|
| R-2 | False "South African data storage" claim | High | Verify R2 region/jurisdiction or remove claim |
| R-4 | Plaintext OTP/reset tokens in RabbitMQ | High | Hash-at-rest + short TTL + purge on consume |
| R-6 | OCR engine not selected | High | Select engine; establish accuracy baseline for SA receipts |
| R-1 | AOT vs. reporting libraries | High | AOT-binary tests before production claim |
| CP-07 | Photos lost on tab close | High | Implement draft persistence (IndexedDB/localStorage) |

## Project State (from §12 of Product Requirements)

- **Backend:** Solution skeleton (2026-10-18); EF Core + auth slice [Reported]; 47 tests [Reported]; **not verified against code**. No outbox relay, email delivery, worker logic, or Realtime hubs.
- **Frontend:** Marketing site, mock auth, dashboard, capture, gallery, reports (CSV/JSON samples), legal pages with mock data only. Updated 2026-11-15.
- **Phase 1 blockers:** JWT permission codes; production-readiness reclassification; DB topology from config; AOT binary against endpoint matrix; 00-INDEX.md update.

## Priority Action Items

### Immediate (0-2 weeks):
1. R-2: Resolve "South African data storage" claim
2. CP-07: Implement draft persistence so photos survive tab close
3. R-4: Fix plaintext OTP tokens in RabbitMQ
4. RP-07/RP-05: Resolve CSV export vs PDF-only conflict

### Short-term (2-4 weeks):
5. Select OCR engine (R-6)
6. Implement email delivery via outbox relay
7. Address 59 failing tests
8. Implement workspace roles (ID-11)

### Medium-term (4-8 weeks):
9. PayFast billing integration (BL-02)
10. Duplicate detection logic (R-7)
11. Complete notification channel abstraction (NT-03)
12. Implement account deletion/erasure (ID-10)

### Long-term (8+ weeks):
13. AOT compatibility verification (R-1)
14. Finalize pricing (R-5)
15. Full end-to-end user flows
16. Full test verification against code

---
| Billing | ~10% | PayFast not integrated; entitlement ambiguous | Medium |
| Workspace | ~30% | Owner/member roles undefined; no API | Medium |
| Frontend | ~40% | Mock data only; needs real backend integration | Low-Medium |
| Tests | 12/71 passing | 59 failed (mostly outbox/publisher) | High |

---

---
| R-3 | Free tier vs CSV export conflict | Direct conflict |
| R-9 | Billing placement ambiguity | Uncertainty |
| R-10 | Verification debt (59 failing tests) | Test reliability |
| R-12 | Frontend crash reporting gap | Monitoring gap |

---

---