---
name: add-endpoint
description: Use when adding or changing an API endpoint and its client call.
---

# add-endpoint

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Run `plan-first`. Confirm the endpoint does not already exist.
2. Contract: request/response records in their own files (or the existing contracts file for one endpoint's small DTOs).
3. Controller in `src/YogaMarketplace.Api/Controllers`: thin; auth attribute, rate-limit policy, call one service method.
4. Service interface + implementation in separate files under `Services`; register in `ServiceRegistration.cs`. Keep classes under ~300 lines; add a new class rather than growing `BookingService`/`ProviderService`.
5. Business values from `IPlatformSettingsService`; city/state/timezone from data or settings.
6. Failures throw `DomainException(status, message, code)` with a stable snake_case code (e.g. `area_invalid`).
7. Client: add the call in `src/services/http` / the feature folder, type it, handle errors by `code`, use TanStack Query.
8. Tests: xUnit success, validation 400, 401/403, wrong-user, and each error code; Vitest for the UI state.
9. No migration unless the plan said so (use `ef-migration`).
