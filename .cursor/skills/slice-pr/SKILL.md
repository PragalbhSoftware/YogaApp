---
name: slice-pr
description: Use when implementing an approved slice: one branch, small commits, no unrelated changes, full PR body.
---

# slice-pr

Follow `.cursor/rules/*.mdc`. No hardcoded city/state/timezone, business rules in admin settings, one type per file, error codes not message text, isolated tests, LF endings, no unrelated changes, squash-merge only after approval.

1. Branch from latest `main`: `git switch main && git pull && git switch -c s<sprint>/<slice>-<short-name>`. Never push to `main`.
2. Make small commits, each building and passing tests (e.g. backend+tests, then client+Vitest, then docs).
3. Touch only what the slice needs: no reformatting, no layout tweaks, no renames outside scope.
4. Keep LF line endings; never let a commit rewrite a whole file. Check `git diff --stat main...HEAD` before each push.
5. Run `dotnet test YogaMarketplace.sln -c Release` and in `src/YogaMarketplace.Client`: `npm run typecheck && npm run lint && npm test && npm run build`.
6. Open the PR with sections: Summary, Tests added, Tests run (only what you ran), Test steps, Staging SQL Server checks, Deploy note (migration? new setting? config?), Known gaps.
7. Wait for review OK from the owner. Merge is squash-merge only, after approval.
