---
name: cd-deploy
description: Use when setting up or changing deployment. Default target: Azure App Service + Azure SQL; alternatives noted.
---

# cd-deploy

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Environments: `dev` (auto on merge to main), `staging`, `prod` as GitHub Environments; `prod` requires manual approval by the owner.
2. Build once: deploy the same `site-publish` artifact from CI to every environment; never rebuild per environment.
3. App Service (Linux or Windows, .NET 8) with a `staging` deployment slot; deploy to the slot, warm up, check `/health/ready`, then swap to production.
4. Migrations are a gated step: take a backup (see `db-backup-restore`), run the idempotent `migrations.sql` artifact against Azure SQL, then swap. Migrations must be expand-then-contract so the old build still works.
5. Config: never deploy or overwrite `web.config` or `appsettings*.json` values on the server; settings live in App Service configuration / slot settings. Never touch the `keys` (DataProtection) or `uploads` folders; on Azure, move them to Blob Storage.
6. Secrets in Azure Key Vault referenced from App Service settings, or GitHub Environment secrets; auth with OIDC (`azure/login` federated credentials), no publish profiles in the repo.
7. After the swap run the `smoke` workflow against the environment URL.
8. Rollback: swap the slots back (seconds); if a migration damaged data, restore the backup (see `docs/ops/rollback.md`).
9. Alternatives: Plesk/IIS (current: zip deploy with `app_offline.htm`, keep `web.config`), AWS Elastic Beanstalk + RDS SQL Server, or containers (Azure Container Apps) with the same gates.
