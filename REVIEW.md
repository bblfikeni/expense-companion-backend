# Working tree review — 19 October 2026

Reviewed tracked and untracked source, configuration, migrations, tests, project
files, service entry points and documentation. Existing in-progress authentication
and persistence work was retained and corrected.

## Bugs fixed

| Priority | Finding | Fix |
| --- | --- | --- |
| Critical | Any authenticated user could read or update another profile by changing `profilePublicId`. | Both profile endpoints check the requested ID against the authenticated subject. |
| High | API configuration contained reusable JWT signing and OTP pepper secrets. | Removed embedded values; startup requires independently configured secrets. |
| High | Concurrent OTP verification could reuse a code or lose failed-attempt increments. Issuance could create overlapping codes. | PostgreSQL transaction locks serialize OTP issuance and verification across contexts and gateway instances. Code generation uses unbiased `RandomNumberGenerator.GetInt32`, avoiding `Math.Abs(int.MinValue)` overflow. |
| High | OTP consumption committed before account verification or password changes; issuance committed before its delivery event. | Account, OTP and outbox writes now share a transaction. Retry attempts reload tracked account state. |
| High | Changing a password's security stamp did not invalidate JWTs. | Tokens carry a stamp checked against the account on each authenticated request. Deleted or anonymized accounts also fail validation. |
| High | Concurrent login failures could lose increments and weaken Identity lockout. | Login attempts serialize per account, including failed-password counter updates. |
| Medium | Registration exposed `Created`; verification resend exposed account-specific cooldown results. Weak-password registrations also distinguished existing accounts. | Public acceptance responses omit existence-sensitive fields; password policy applies before checking registration status. |
| Medium | Password recovery issued unlimited consecutive codes. A weak replacement password consumed a valid reset code. | Recovery respects the configured cooldown; password validation precedes OTP consumption. Successful resets clear password lockout. |
| Medium | Registration names and email addresses exceeding database limits could cause server errors. Invalid profile updates returned misleading 404 responses. | Validate limits before persistence and return 400 for invalid display names. |
| Medium | The custom context factory created scopes that were never disposed. | Use EF's built-in factory with singleton options and scoped request contexts. |
| Medium | Identity's token providers lacked Data Protection registration in non-web hosts. | Register Data Protection alongside Identity. |
| Medium | API health always returned success, including during database outages. | Map the registered PostgreSQL health check. |
| Low | API ran the same application twice and globally suppressed obsolete OpenAPI warnings. | Remove the second `Run` and obsolete `WithOpenApi` calls; remove the warning suppression. |
| Low | Missing-connection error named the wrong environment variable. | Report `ConnectionStrings__CompanionDb`. |
| Low | Tests inherited ambient configuration, resolved health options incorrectly, counted PostgreSQL system constraints, and injected secrets after validation. Outbox helpers could read another account's message. | Isolate missing-config tests, resolve `IOptions`, limit constraint queries to the application schema, inject test settings early, and select delivery messages by profile ID. |

## Unfinished functionality identified

- **Email delivery is not operational:** there is no outbox relay, RabbitMQ
  publisher/consumer, or email provider. Auth endpoints enqueue delivery events.
- Notification, Extraction, Reporting and Scheduler workers are idle shells.
- Realtime has no SignalR hubs; its health route is static liveness only.
- The Next.js client/submodule has not been added; its repository URL is missing.
- Seq is provisioned, but application logs currently use the console only.
- Worker schema verification is implemented but not wired into the idle workers.
- Delivery payloads contain plaintext OTPs; outbox retention is unimplemented.

These require feature specifications, a client repository URL or delivery-provider
configuration. They remain documented implementation gaps.

## Operational changes

Configure `Auth__Jwt__SigningKey` and `Auth__Otps__Pepper` before starting the API;
see README for generation commands. Existing tokens without a security stamp must
be replaced by logging in. Bearer validation now reads account state from
PostgreSQL. Registration and resend response bodies intentionally omit internal
creation/cooldown flags. API `/health` uses the standard health-check response.

## Validation

The original baseline had 5 failures out of 34 tests. Regression coverage now
includes cross-account profile access, concurrent registration, OTP single use and
attempt limits, login lockout, recovery cooldown, password-reset token revocation,
and injected database failures verifying account/OTP/outbox rollback.

Final checks: **47 tests passed, 0 failed, 0 skipped** against PostgreSQL 17
Testcontainers. The full solution Release build passed with **0 warnings and 0
errors**. `git diff --check` passed. No changes were committed.
