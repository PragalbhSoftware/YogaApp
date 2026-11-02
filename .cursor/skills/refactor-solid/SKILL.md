---
name: refactor-solid
description: Use when a class passes ~300 lines or mixes jobs (e.g. BookingService, ProviderService); behaviour must not change.
---

# refactor-solid

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Do it as its own slice, never inside a feature PR.
2. Map responsibilities (orders, credits, home-city policy, cancel/reschedule, webhooks) and the public methods each uses.
3. Make sure tests cover the current behaviour; add characterization tests first if missing.
4. Extract one responsibility at a time into a new interface + class (separate files), injected via `ServiceRegistration.cs`; the old class delegates.
5. Keep public API contracts and error codes identical; no logic changes.
6. One commit per extraction; full suites green after each.
7. Keep line endings; diff should show moves, not whole-file rewrites.
8. PR body lists each extraction and states "no behaviour change".
