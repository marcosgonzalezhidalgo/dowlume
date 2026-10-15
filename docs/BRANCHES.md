# Branch policy

`main` is the default and only permanent branch. It contains the complete application and the README shown on the repository home page.

## Contribution workflow

1. Pull `main` with `git pull --ff-only origin main`.
2. Create a short-lived `feature/`, `fix/`, `docs/`, or `chore/` branch.
3. Make focused Conventional Commits and run the checks relevant to the change.
4. Push the branch and open a PR into `main`.
5. Review the diff, resolve any failing checks, and merge.
6. Delete the merged branch and synchronize local `main`.

```powershell
git switch main
git pull --ff-only origin main
git switch -c docs/describe-the-change
```

Do not force-push shared history. Ordinary topic PRs may be squashed into a descriptive Conventional Commit; use merge commits when preserving a dependent branch stack. GitHub automatically deletes merged branches. See [Contributing](../CONTRIBUTING.md) for validation requirements.

## Historical reference

The initial implementation was integrated through [PRs #1–#5](https://github.com/marcosgonzalezhidalgo/dowlume/pulls?q=is%3Apr+is%3Amerged+sort%3Acreated-asc) on 2026-10-15, preserving the original commits. Inspect it with `git log --graph --oneline --decorate main`. Revision comparisons and release-candidate evidence are retained in [Validation](VALIDATION.md); public `v0.1.0` publication remains subject to the [release checklist](RELEASE_CHECKLIST.md).
