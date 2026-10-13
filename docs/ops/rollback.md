# Rollback

For the single IIS/Plesk site (see README → Deploy to Plesk). Roll back when `/health/ready` is not `200` or the smoke test fails after a deploy, and the fix is not a one-line config change.

## Before every deploy

1. **Keep the last 3 publish zips.** Download the `site-publish` artifact from the CI run you deploy and save it outside the document root, for example `...\private\yogademo\releases\2026-10-13_2475862.zip` (date and short commit). Delete the oldest when there are more than 3. CI artifacts expire, so don't rely on them.
2. **Back up the database** before running `migrations-sql`. In Plesk: **Databases → Export Dump**, or in SSMS:

   ```sql
   BACKUP DATABASE [<db>] TO DISK = N'<backup folder>\<db>_2026-10-13_2475862.bak' WITH COPY_ONLY, INIT;
   ```

   Name it like the zip so you know which release it came before.

## Migrations: expand, then contract

The previous build must run against the new schema, so rolling back the app never needs a database restore.

- **Expand (release N):** add new columns as nullable or defaulted, add new tables, and write to old and new columns where needed. Never rename or drop something the previous build reads.
- **Contract (release N+1 or later):** once release N is stable and won't be rolled back, drop or tighten what nothing reads anymore.
- Every migration keeps a working `Down()`, but in production you roll back by restoring the backup (below), not by running `Down()`.

## Roll back the app

1. Upload `deploy/plesk/app_offline.htm` to the document root.
2. Unzip the previous release zip and upload its files over the site, **except `web.config`** (the server copy holds the variables). Leave the `keys` and `uploads` folders alone.
3. Delete `app_offline.htm`.
4. Check `https://yogademo.psoftcs.com/health/ready` returns `200`, then run **Actions → smoke** against the site.

With expand-then-contract migrations, the database stays as it is.

## Restore the database (a migration went wrong)

Only when a migration failed half-way or damaged data. Anything written after the backup is lost, so note the time and tell the team.

1. Upload `app_offline.htm`, so nothing writes while you restore.
2. Restore the backup taken before the release. In Plesk: **Databases → Import Dump**. In SSMS:

   ```sql
   USE master;
   ALTER DATABASE [<db>] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
   RESTORE DATABASE [<db>] FROM DISK = N'<backup folder>\<db>_2026-10-13_2475862.bak' WITH REPLACE;
   ALTER DATABASE [<db>] SET MULTI_USER;
   ```

3. Deploy the previous release zip (the app rollback above, steps 2 to 4).
4. Check Razorpay: any payment captured between the backup and the restore is in Razorpay but not in the database. Find those payments in the Razorpay dashboard and refund or rebook them by hand.
5. Fix the migration in a new PR; never edit one that has already run anywhere.

## Practice run

Do this once on staging, and again whenever these steps change: deploy a release, roll back to the previous zip, restore the database backup into a copy (for example `<db>_restore_test`) to prove the backup is usable, then deploy the latest release again and run the smoke test.
