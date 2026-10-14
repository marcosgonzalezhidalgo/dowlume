# Dowlume roadmap

Status: the Windows MVP is implemented as a release candidate. Release approval and publication remain separate gates. Evidence is recorded in [VALIDATION.md](docs/VALIDATION.md).

## Milestone 0: Foundation

- [x] C# solution with Avalonia, Core, Infrastructure, Monitoring, and xUnit projects.
- [x] Constructor-based composition, formatting, structured diagnostics, and CI configuration.
- [x] Domain state machine and explicit safety requirements.
- [x] MIT license and contribution conventions.
- [ ] Hosted GitHub Actions verified on reviewed pull requests.

## Milestone 1: Safe detection

- [x] Configurable local Downloads folder with Windows known-folder suggestion.
- [x] FileSystemWatcher creation/change/rename hints.
- [x] Bounded queue, duplicate-event handling, and a single consumer.
- [x] Conservative readiness checks and browser partial-file exclusions.
- [x] Startup/periodic reconciliation and nonrecursive discovery.
- [x] Automated burst, overflow, rename, locked-file, and restart checks.

## Milestone 2: Classification

- [x] Configurable categories and extension matching.
- [x] On-demand category folders, no-overwrite moves, numeric collision suffixes.
- [x] Dry-run preview and explicit consent before monitoring.
- [x] SQLite operation journal and disposable-filesystem integration tests.
- [x] Failure and cancellation behavior preserves existing content.

## Milestone 3: Lifecycle

- [x] Persist first-seen time, category, paths, status, and content hash.
- [x] Configurable category age thresholds.
- [x] Expiring Files holding area.
- [x] Persistent Keep, seven-day Snooze, explicit Move, and Windows Recycle actions.
- [x] Idempotent processing and protected undo semantics.

## Milestone 4: Desktop experience

- [x] Overview, preview, review, activity, and settings screens.
- [x] Folder pickers and confirmation dialogs.
- [x] Progress, errors, journal details, and recovery controls.
- [x] Tray hide/reopen/exit and orderly shutdown.
- [x] Headless end-to-end workflow and rendering checks.

## Milestone 5: Hardening and release

- [x] Interrupted journal recovery and ambiguous-copy protection.
- [x] Content-change, collision, locked-source, and native recycle safety tests.
- [x] Structured path-free diagnostic log with bounded rotation.
- [x] Self-contained Windows ZIP and per-user installer build scripts.
- [x] Usage, architecture, contribution, branch, and release documentation.
- [ ] Clean-machine install/upgrade/uninstall acceptance testing.
- [ ] Physical cross-volume interruption and constrained Recycle Bin testing.
- [ ] Review and merge PRs, verify hosted checks, and approve the release.
- [ ] Sign when a certificate is available, publish binaries/checksums, and tag `v0.1.0`.

## Post-MVP

- Duplicate detection and storage insights.
- Richer classification rules and virtual organization.
- Multi-folder simultaneous monitoring.
- Following external renames and explicitly accepting edited tracked content.
- Incremental database queries and history pagination for very large folders.
- Optional Windows startup integration.
- Linux/macOS platform implementations, localization, and broader accessibility review.

No AI classification, cloud synchronization, arbitrary scripts, or permanent automatic deletion is included in this MVP.
