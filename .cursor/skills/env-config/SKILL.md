---
name: env-config
description: Use when adding configuration, environment values, secrets or feature flags.
---

# env-config

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Layering: `appsettings.json` (safe defaults) -> `appsettings.{Environment}.json` -> user-secrets (local) -> environment variables / Key Vault (servers). Later wins.
2. Never commit keys, connection strings with passwords, Razorpay secrets, SMS/WhatsApp tokens or JWT signing keys. Use `dotnet user-secrets` locally.
3. Bind config to typed options in `src/YogaMarketplace.Api/Options` (one class per file) with validation on start; `ProductionSafety` fails fast on unsafe prod values.
4. Razorpay: separate test and live key sets per environment; only prod uses live keys; staging/dev must refuse live keys. Webhook secret per environment.
5. Business values are admin settings (`add-config-rule`), not appsettings.
6. Feature flags: a boolean in `PlatformSettings` (admin-toggled) or `Microsoft.FeatureManagement` for deploy-time flags; default off; remove after rollout.
7. Client: only `VITE_*` public values; never put secrets in the client build.
8. Document every new key in the README config table and the PR Deploy note.
