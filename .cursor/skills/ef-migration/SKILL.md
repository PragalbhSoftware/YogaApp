---
name: ef-migration
description: Use for any database schema or seed data change.
---

# ef-migration

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Change entities/`ModelConfiguration` first; one entity per file for new types.
2. Add: `dotnet ef migrations add <Name> --project src/YogaMarketplace.Infrastructure --startup-project src/YogaMarketplace.Infrastructure`.
3. Expand then contract: new columns nullable or defaulted; never rename or drop what the previous build reads in the same release.
4. Clean duplicates before a unique index; data updates use `WHERE` on the old value so they re-run safely.
5. `Down()` must work. Never edit a merged migration; add a new one.
6. Generate the script: `dotnet ef migrations script --idempotent ... -o artifacts/migrations.sql` and read it.
7. Add a migration test (see `*MigrationTests.cs`) and a `[SqlServerFact]` test when SQLite can't prove it.
8. PR Deploy note: "migration: yes", backup required before running it (see `db-backup-restore`).
