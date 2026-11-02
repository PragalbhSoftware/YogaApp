---
name: security-check
description: Use on every change that touches an endpoint, auth, payments, uploads, logging or config.
---

# security-check

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Auth: every endpoint has `[Authorize]` with the right role, or is deliberately anonymous; admin controllers inherit `AdminControllerBase`.
2. Ownership: any id from the route/body is checked against the current user (customer owns booking, provider owns slot). Add a wrong-user test.
3. Input: validate requests; bounds on strings, lists, pages; reject unknown enum values and bad GUIDs with 400.
4. Errors: return the standard error body with a `code`; never stack traces or SQL.
5. Rate limits: OTP, login, refresh, bookings, payments, packs and uploads use a policy in `Security/RateLimiting.cs`.
6. Payments: verify Razorpay signatures; webhooks idempotent; amounts in paise (`long`), recomputed on the server.
7. Uploads: check type and size, random names, stored under `uploads` outside source control.
8. Secrets: none in code, appsettings, tests or logs; use user-secrets locally, Key Vault or env vars on servers.
9. Logging: never log OTP, tokens, full phone or email; use `StructuredLogging` masking.
10. Admin actions write an audit row (who, what, when).
11. Client: no `dangerouslySetInnerHTML`, no tokens in localStorage beyond the existing auth store design.
