# Initial implementation branch stack

The pre-existing F#-to-C# conversion was preserved in the foundation commit. No existing commits were rewritten and `main` was not bypassed with an unreviewed merge.

| Branch | Base | Review scope |
| --- | --- | --- |
| [`chore/project-setup` (PR #1)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/1) | `main` | C# conversion, domain records/rules, SDK pin, CI, initial tests. |
| [`feature/safe-file-operations` (PR #2)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/2) | `chore/project-setup` | SQLite, journal, verified moves, lifecycle decisions, recovery, undo. |
| [`feature/file-monitoring` (PR #3)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/3) | `feature/safe-file-operations` | Watcher, bounded queue, readiness, periodic scans, monitoring tests. |
| [`feature/review-center` (PR #4)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/4) | `feature/file-monitoring` | Desktop screens, consent, settings, tray, headless workflow. |
| [`release/0.1.0` (PR #5)](https://github.com/marcosgonzalezhidalgo/dowlume/pull/5) | `feature/review-center` | Safety hardening, packaging, documentation, release validation. |

Each branch is cumulative. Use `git log --oneline --decorate --all` to inspect the exact commits and `git diff <base>..<branch>` for a focused review. The final release branch is the working branch for the complete implementation.

All five topic branches are pushed and have dependent draft PRs. Review them in this order and retarget each child after its prerequisite merges. Keep `main` as the only permanent branch after merging. Tag `v0.1.0-rc.1` identifies tested application revision `9322acc`; do not publish a final `v0.1.0` tag until the remaining clean-machine release checklist passes.
