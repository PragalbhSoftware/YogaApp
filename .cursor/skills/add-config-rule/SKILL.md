---
name: add-config-rule
description: Use when a change needs a fee, percentage, limit, time window, city/timezone or other business value.
---

# add-config-rule

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Never hardcode it. Check `PlatformSettings` (Domain) for an existing field first.
2. Add the field to `PlatformSettings` with a safe default and a nullable/defaulted column (run `ef-migration`).
3. Validate it in `PlatformSettingsRules` (range, required) with a test for each bound.
4. Expose it in `CurrentSettings` and the admin settings request/response; `AdminSettingsController` updates write a `SettingsAudit` row.
5. Read it through `IPlatformSettingsService.GetAsync` (60 s cache) in services; never read raw config for business values.
6. Add the admin UI field in `features/admin` with Zod validation matching the server rules.
7. Tests: default value, admin update + audit row, rule uses the new value, invalid value 400.
8. Note the new setting in the PR Deploy note.
