---
name: ci-pipeline
description: Use when setting up or changing GitHub Actions CI and repo protections.
---

# ci-pipeline

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Keep `.github/workflows/ci.yml` jobs: `build` (dotnet test with SQL Server service), `migrations` (idempotent script artifact), `publish` (site zip with SPA in wwwroot), `client` (typecheck, lint, test, build).
2. Add format checks: `dotnet format --verify-no-changes`; client lint via `oxlint`.
3. Line endings: commit `.gitattributes` (`* text=auto eol=lf`, `*.png binary` etc.) and `.editorconfig` `end_of_line = lf`; fail CI if `git ls-files --eol` shows CRLF in the index.
4. Secret scan: gitleaks action on push and PR (full history on the first run).
5. Dependency scan: `dotnet list package --vulnerable --include-transitive` (fail on High) and `npm audit --audit-level=high`.
6. CodeQL workflow for `csharp` and `javascript-typescript`.
7. `.github/dependabot.yml`: nuget, npm (`/src/YogaMarketplace.Client`), github-actions, weekly, grouped minor/patch.
8. Branch protection on `main`: PR required, 1 approval, required checks (build, client, publish, migrations, codeql, gitleaks), squash-merge only, no force push.
9. Pin action versions; set `permissions: contents: read` by default.
