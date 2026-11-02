---
name: review-pr
description: Use before pushing or when asked to review a PR: self-review the diff against the YogaApp checklist.
---

# review-pr

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. `git diff --stat main...HEAD`: any file with far more changed lines than intended means line-ending or format churn; revert it.
2. Scope: every changed file belongs to the slice; no unrelated refactors or UI changes.
3. Rules: no new hardcoded city, state, timezone, fee, limit or window; business values come from `PlatformSettings`.
4. One type per file; interface and implementation in separate files.
5. Errors: domain failures throw `DomainException` with a stable `code`; client matches `code`, never message text.
6. Server enforces every rule; client flags (`?remote=1`, hidden buttons) are UX only.
7. Tests: success, failure, auth (401/403/wrong user), boundary; isolated data; no real sleeps.
8. Security: run the `security-check` skill.
9. PR body matches what was actually run. Report OK or CHANGES with file:line for each item.
