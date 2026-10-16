# Dowlume roadmap

**Current state:** the Windows MVP is implemented as a 0.1.0 release candidate. A stable public release is still pending. Evidence is recorded in [VALIDATION.md](docs/VALIDATION.md).

| Stage | Status | Details |
| --- | --- | --- |
| Windows MVP | Implemented | [Completed work](#completed-windows-mvp) |
| Public 0.1.0 release | Pending acceptance and approval | [Release gates](#remaining-for-public-010) |
| Post-MVP | Ideas, not delivery commitments | [Future work](#future-work) |

## Remaining for public 0.1.0

- [ ] Clean-machine installation, first-use, upgrade, and uninstall acceptance testing.
- [x] Physical cross-volume move, undo, and interrupted-copy recovery testing on separate NVMe/SATA disks.
- [ ] Disabled/full Recycle Bin testing in an isolated Windows environment.
- [ ] Independent release approval after integration and acceptance checks.
- [ ] Decide on signing, sign when a certificate is available, and publish release notes, binaries, and checksums with tag `v0.1.0`.

The [release checklist](docs/RELEASE_CHECKLIST.md) is the detailed acceptance record. Local package generation and automated tests do not complete these gates.

Current work is limited to Windows release hardening. Clean-machine testing is unavailable, so the stable release remains unpublished. The [acceptance kit and procedure](docs/WINDOWS_ACCEPTANCE.md) prepare the remaining tests; new features and Linux/macOS work stay deferred.

## Completed Windows MVP

### Milestone 0: Foundation

- [x] C# solution with Avalonia, Core, Infrastructure, Monitoring, and xUnit projects.
- [x] Constructor-based composition, formatting, structured diagnostics, and CI configuration.
- [x] Domain state machine and explicit safety requirements.
- [x] MIT license and contribution conventions.
- [x] Hosted GitHub Actions build/test/format checks verified for the release candidate.

### Milestone 1: Safe detection

- [x] Configurable local Downloads folder with Windows known-folder suggestion.
- [x] FileSystemWatcher creation/change/rename hints.
- [x] Bounded queue, duplicate-event handling, and a single consumer.
- [x] Conservative readiness checks and browser partial-file exclusions.
- [x] Startup/periodic reconciliation and nonrecursive discovery.
- [x] Automated burst, overflow, rename, locked-file, and restart checks.

### Milestone 2: Classification

- [x] Configurable categories and extension matching.
- [x] On-demand category folders, no-overwrite moves, numeric collision suffixes.
- [x] Dry-run preview and explicit consent before monitoring.
- [x] SQLite operation journal and disposable-filesystem integration tests.
- [x] Failure and cancellation behavior preserves existing content.

### Milestone 3: Lifecycle

- [x] Persist first-seen time, category, paths, status, and content hash.
- [x] Configurable category age thresholds.
- [x] Expiring Files holding area.
- [x] Persistent Keep, seven-day Snooze, explicit Move, and Windows Recycle actions.
- [x] Idempotent processing and protected undo semantics.

### Milestone 4: Desktop experience

- [x] Overview, preview, review, activity, and settings screens.
- [x] Folder pickers and confirmation dialogs.
- [x] Progress, errors, journal details, and recovery controls.
- [x] Tray hide/reopen/exit and orderly shutdown.
- [x] Headless end-to-end workflow and rendering checks.

### Milestone 5: Hardening and release preparation

- [x] Interrupted journal recovery and ambiguous-copy protection.
- [x] Content-change, collision, locked-source, and native recycle safety tests.
- [x] Structured path-free diagnostic log with bounded rotation.
- [x] Self-contained Windows ZIP and per-user installer build scripts.
- [x] Usage, architecture, contribution, branch, and release documentation.
- [x] Merge the five implementation PRs into `main`, preserving commit and PR history.

## Development after the MVP

- [x] Search all recorded operations by filename or path, action, and status.
- [x] Browse older history in bounded pages while preserving undo checks and the global attention count.
- [x] Assess [macOS and Linux prerequisites](docs/PLATFORM_SUPPORT.md); native implementations and validation remain deferred.

## Future work

These are possible directions after the MVP release, without scheduled versions or dates.

- Duplicate detection and storage insights.
- Richer classification rules and virtual organization.
- Multi-folder simultaneous monitoring.
- Following external renames and explicitly accepting edited tracked content.
- Incremental organizer/recovery queries and indexed history searches for very large folders.
- Optional Windows startup integration.
- [Linux/macOS platform implementations](docs/PLATFORM_SUPPORT.md), localization, and broader accessibility review.

No AI classification, cloud synchronization, arbitrary scripts, or permanent automatic deletion is included in this MVP.
