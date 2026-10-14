# Initial implementation branch stack

The pre-existing F#-to-C# conversion was preserved in the foundation commit. No existing commits were rewritten and `main` was not bypassed with an unreviewed merge.

| Branch | Base | Review scope |
| --- | --- | --- |
| `chore/project-setup` | `main` | C# conversion, domain records/rules, SDK pin, CI, initial tests. |
| `feature/safe-file-operations` | `chore/project-setup` | SQLite, journal, verified moves, lifecycle decisions, recovery, undo. |
| `feature/file-monitoring` | `feature/safe-file-operations` | Watcher, bounded queue, readiness, periodic scans, monitoring tests. |
| `feature/review-center` | `feature/file-monitoring` | Desktop screens, consent, settings, tray, headless workflow. |
| `release/0.1.0` | `feature/review-center` | Safety hardening, packaging, documentation, release validation. |

Each branch is cumulative. Use `git log --oneline --decorate --all` to inspect the exact commits and `git diff <base>..<branch>` for a focused review. The final release branch is the working branch for the complete implementation.

Before publication, push the topic branches, open dependent PRs in this order, run hosted checks, and obtain review. Keep `main` as the only permanent branch after merging. A local release-candidate tag identifies tested source; do not publish a final `v0.1.0` tag until the remaining clean-machine release checklist passes.
