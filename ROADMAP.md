# Dowlume — Development Roadmap

> **Status:** Planning. Milestone estimates assume roughly 8–12 hours per week. Dates and scope may change as the project develops.

## Product goal

Deliver a reliable Windows desktop application that automatically organizes completed downloads into category subfolders and moves aging files into a safe, user-controlled review queue.

## Milestone 0 — Project foundation (Week 1)

- [ ] Create the .NET solution and Git repository structure.
- [ ] Add Avalonia desktop project, core library, infrastructure library, and xUnit tests.
- [ ] Configure dependency injection, logging, formatting, and CI build/tests.
- [ ] Define the file state machine and safety requirements.
- [ ] Choose a license and contribution conventions.

**Done when:** The solution builds in CI and launches a minimal desktop window.

## Milestone 1 — File discovery and monitoring (Weeks 2–3)

- [ ] Configure a Downloads directory (with sensible detection and manual override).
- [ ] Watch file creation, changes, and renames with `FileSystemWatcher`.
- [ ] Deduplicate events and queue files for bounded processing.
- [ ] Implement a conservative file-readiness policy; handle temporary download extensions.
- [ ] Add periodic reconciliation scans to recover missed events and restarts.
- [ ] Ignore application-managed directories and prevent event loops.

**Done when:** New, completed test downloads are reliably discovered, including bursts of files and simulated restarts.

## Milestone 2 — Safe classification (Weeks 4–5)

- [ ] Map file types to default categories and allow overrides.
- [ ] Create category subfolders as needed.
- [ ] Move files safely within Downloads; resolve collisions without overwriting.
- [ ] Handle locked files, permissions, and missing destination directories.
- [ ] Write operation records to SQLite.
- [ ] Add integration tests using disposable temporary directories.

**Done when:** Files move into the intended subfolders with no silent loss or overwrites in automated tests.

## Milestone 3 — Lifecycle and review queue (Weeks 6–7)

- [ ] Define configurable age thresholds per category.
- [ ] Persist the moment Dowlume first observes or imports a file.
- [ ] Run periodic age checks independently of filesystem events.
- [ ] Move eligible files into `Expiring Files` while preserving origin metadata.
- [ ] Implement Keep, Snooze, Move and Trash actions.
- [ ] Ensure reviewed files do not immediately re-enter the review queue.

**Done when:** Files enter review at the configured threshold, stay until acted on, and can be handled safely.

## Milestone 4 — Desktop experience (Weeks 8–10)

- [ ] Build a dashboard showing categories, review count, and recent activity.
- [ ] Build a review screen with file details and safe action confirmations.
- [ ] Add settings for folders, categories, exclusions, and review periods.
- [ ] Display clear progress and errors for background operations.
- [ ] Support minimize-to-tray/background use where feasible on Windows.

**Done when:** A user can configure and operate the core product without editing files or using the terminal.

## Milestone 5 — Reliability and MVP release (Weeks 11–14)

- [ ] Implement undo for supported move/rename operations.
- [ ] Test duplicate names, large files, rapid download batches, and unusual filenames.
- [ ] Test crash/restart recovery and external changes to managed files.
- [ ] Add an explicit first-run consent flow before organizing existing Downloads.
- [ ] Add end-to-end tests and structured diagnostic logging.
- [ ] Package a Windows release and publish setup instructions.

**Done when:** A release candidate passes the safety checklist and can be installed and used on a clean Windows machine.

## Post-MVP backlog

- [ ] Dry-run preview for historical cleanup and complex rule changes.
- [ ] Duplicate detection with hashes and safe review of duplicates.
- [ ] Rules based on names, sizes, metadata, and PDF contents.
- [ ] Storage statistics and review recommendations.
- [ ] Virtual organization mode that leaves files in place.
- [ ] Optional plugins for additional classification/actions.
- [ ] Linux and macOS support after platform-specific testing.
- [ ] Localization and accessibility improvements.

## Non-goals for MVP

- No AI-based file classification.
- No cloud sync, online accounts, or telemetry requirement.
- No automatic permanent deletion.
- No scanning or rearranging arbitrary system folders by default.
- No compression or conversion of original files.

## Minimum safety checklist

- [ ] No file is overwritten without explicit user intent.
- [ ] Processing never starts solely because a `Created` event was received.
- [ ] A missed watcher event is recoverable by reconciliation.
- [ ] Every managed move has a persisted record.
- [ ] Crash recovery does not produce duplicate or destructive operations.
- [ ] File actions are limited to configured folders unless the user explicitly chooses another destination.
- [ ] Trash behavior is verified against the supported OS.
- [ ] Unexpected errors preserve the source file and are visible to the user.
