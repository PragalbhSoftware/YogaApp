---
name: observability
description: Use when adding logging, metrics, alerts or health checks, or when deploying to the cloud.
---

# observability

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Structured logs via Serilog (`Logging/StructuredLogging.cs`); message templates with named properties, event ids from `ErrorEvents.cs`; mask OTP, tokens, phone, email.
2. Correlation: include trace id in every log and in error responses.
3. Cloud: add Azure Monitor OpenTelemetry (`Azure.Monitor.OpenTelemetry.AspNetCore`) or Application Insights; connection string from config, never committed.
4. Track key metrics: bookings created, payments failed, webhook failures, OTP send failures, job runs and failures.
5. Health: `/health` (liveness) and `/health/ready` (database and dependencies, tagged `ready`); App Service health check points at `/health/ready`.
6. Alerts: 5xx rate, `/health/ready` failing, payment/webhook failures, job failures, DB DTU/CPU high; route to the owner by email/WhatsApp.
7. Log retention and PII: keep 30-90 days; no full PII.
8. Test: captured-log test proving nothing sensitive leaks.
