# Git history and branch policy

`main` is the default and only permanent branch. It contains the complete application and the README shown on the repository home page.

## Completed implementation

PRs #1 through #5 were merged into `main` in dependency order on 2026-10-15 after their CI checks passed. Each dependent PR was retargeted to `main` before merging. Merge commits preserve the original feature commits and the pre-existing F#-to-C# conversion.

| Merged branch and PR | Original dependency | Scope |
| --- | --- | --- |
| [`chore/project-setup` (PR #1)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/1) | `main` | C# conversion, domain records/rules, SDK pin, CI, initial tests. |
| [`feature/safe-file-operations` (PR #2)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/2) | `chore/project-setup` | SQLite, journal, verified moves, lifecycle decisions, recovery, undo. |
| [`feature/file-monitoring` (PR #3)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/3) | `feature/safe-file-operations` | Watcher, bounded queue, readiness, periodic scans, monitoring tests. |
| [`feature/review-center` (PR #4)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/4) | `feature/file-monitoring` | Desktop screens, consent, settings, tray, headless workflow. |
| [`release/0.1.0` (PR #5)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/5) | `feature/review-center` | Safety hardening, packaging, documentation, release validation. |

The integrated source tree at `43992f3` was verified to match the tested release branch at `16facb0` exactly. The merged topic branches can be deleted without losing their commits or PR discussions. Use `git log --graph --oneline --decorate main` to inspect the retained history.

Tag `v0.1.0-rc.1` continues to identify tested application revision `9322acc`; it has not been moved. Final `v0.1.0` publication remains subject to the release checklist.

## Future work

1. Pull `main` with `git pull --ff-only origin main`.
2. Create a short-lived `feature/`, `fix/`, `docs/`, or `chore/` branch.
3. Make focused Conventional Commits and run the checks relevant to the change.
4. Push the branch and open a PR into `main`.
5. Review the diff, resolve any failing checks, and merge.
6. Delete the merged branch and synchronize local `main`.

Automatic deletion of merged PR branches is enabled on GitHub. A separate permanent `develop` branch is unnecessary for the current workflow. Repository visibility and access remain managed independently of the default branch.
