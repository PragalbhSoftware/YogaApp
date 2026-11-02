---
name: fix-ci
description: Use when a GitHub Actions run (ci, smoke) fails.
---

# fix-ci

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Open the failing job (`build`, `migrations`, `publish`, `client`) and read the first real error, not the last line.
2. Reproduce locally with the same command: `dotnet test YogaMarketplace.sln -c Release`, `dotnet ef migrations script --idempotent ...`, `dotnet publish src/YogaMarketplace.Api -c Release -o publish`, or `npm ci && npm run typecheck && npm run lint && npm test && npm run build`.
3. SQL Server tests need `YOGA_TEST_SQLSERVER`; run docker-compose SQL Server locally if needed.
4. Find the root cause; fix code or test. Never skip, delete or `[Fact(Skip)]` a test, and never weaken an assertion to pass.
5. Flaky? Find the shared state, time or ordering cause and fix isolation.
6. Push the fix as a small commit and confirm the run is green.
