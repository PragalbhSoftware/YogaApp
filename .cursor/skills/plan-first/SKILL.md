---
name: plan-first
description: Use before any code change in YogaApp: read the related code, write a short plan, and wait for the owner's OK before editing.
---

# plan-first

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Restate the slice goal in one line and list what is out of scope.
2. Read the related code first: controller in `src/YogaMarketplace.Api/Controllers`, service in `Services`, entities in `src/YogaMarketplace.Domain`, `YogaDbContext`/`ModelConfiguration`, client feature in `src/YogaMarketplace.Client/src/features/<area>`, and existing tests.
3. Check `.cursor/rules/*.mdc` and `AGENTS.md`; quote any rule the change touches (settings not constants, no hardcoded city/state/timezone, one type per file).
4. Write the plan: files to change, new types (one per file), settings or migration needed, error codes, tests (success + failure + auth), and commit order.
5. Call out risks: money, time zones, concurrency, data migration, deploy impact (`web.config`, `appsettings*`, `keys`, `uploads`).
6. Stop and ask "OK to proceed?". Do not edit until the owner says OK.
