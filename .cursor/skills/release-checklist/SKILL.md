---
name: release-checklist
description: Use before and after every deploy to staging or prod.
---

# release-checklist

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Pre-deploy: CI green on the exact commit; PR approved and squash-merged; release notes list migrations, new settings and config changes.
2. Confirm new settings/env vars exist in the target environment (Key Vault / App Service settings) before deploying.
3. Back up the database (COPY_ONLY or Azure point-in-time noted with timestamp) before any migration.
4. Keep the previous artifact (last 3 releases) for rollback.
5. Deploy to staging slot; `/health` and `/health/ready` return 200.
6. Run the `smoke` workflow; then a manual check: OTP login, browse in the selected city, book Home/Studio/Online, Razorpay test payment, admin dashboard.
7. Swap/approve prod; repeat health check and smoke.
8. Post-deploy: watch errors, latency and failed payments for 30 minutes; confirm background jobs ran.
9. If health or smoke fails and the fix is not a one-line config change, roll back (see `cd-deploy`).
