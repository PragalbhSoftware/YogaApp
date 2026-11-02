---
name: db-backup-restore
description: Use before migrations, when setting backup policy, or when running a restore drill.
---

# db-backup-restore

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Azure SQL: automated backups with point-in-time restore (set retention, e.g. 14-35 days) and long-term retention weekly/monthly for prod.
2. Before every migration: note the UTC timestamp (PITR) or take a `COPY_ONLY` backup / BACPAC export named `<db>_<date>_<commit>`.
3. Plesk/IIS alternative: Plesk Export Dump or `BACKUP DATABASE ... WITH COPY_ONLY, INIT` (see `docs/ops/rollback.md`).
4. Uploads: back up the `uploads` folder or Blob container with soft delete/versioning; DataProtection `keys` too.
5. Restore drill monthly: restore to a new database (never over prod), point staging at it, run `/health/ready` and smoke, record time taken.
6. Real restore: put the app offline (`app_offline.htm` or stop slot), restore, run health + smoke, tell the team what data after the backup was lost.
7. Never store backups or connection strings in the repo.
