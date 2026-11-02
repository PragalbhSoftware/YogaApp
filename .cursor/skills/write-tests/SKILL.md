---
name: write-tests
description: Use when adding tests for a feature or fix in xUnit or Vitest.
---

# write-tests

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Each new behaviour has a test that fails before the change.
2. Backend: integration tests via `YogaApiFactory`; SQL Server-only behaviour via `[SqlServerFact]` + `SqlServerYogaApiFactory` (`YOGA_TEST_SQLSERVER`).
3. Isolation: a test that changes shared data (cities, areas, provider status, settings) uses its own factory/database (pattern: `OtpPhoneLimitTests`) or cleans up; never rely on order.
4. Cover: success, validation 400, 401/403 for admin, wrong-user, each error `code`, and boundaries (exact limit and one either side, IST/UTC day line).
5. Webhooks and jobs: run the same event/job twice.
6. Time: inject `TimeProvider`; client uses fake timers. No real sleeps.
7. Client: Vitest + React Testing Library next to the file (`*.test.tsx`); assert what the user sees.
8. Run the full suites before pushing; a flaky test is a bug.
