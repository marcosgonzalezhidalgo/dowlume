# Dowlume — Getting Started

> **Status:** Planning · **Target:** Windows-first desktop MVP · **Language:** English

## 1. Project overview

**Dowlume** is a local-first desktop application that keeps the Downloads folder organized without taking control away from its owner.

The application will detect completed downloads, classify files into subfolders inside Downloads, and flag aging files for review. Users can then decide whether to **keep**, **snooze**, **move**, or **send files to the recycle bin**.

**Product principle:** Organize → Review → Decide. Never permanently delete user files by default.

## 2. Decisions for the first version

| Area | Decision |
| --- | --- |
| Primary language | C# / .NET |
| Desktop UI | Avalonia UI with MVVM and XAML |
| Target OS | Windows first; other desktop OS considered later |
| Local data | SQLite (via Entity Framework Core if useful) |
| File monitoring | `FileSystemWatcher` plus periodic reconciliation scans |
| Background processing | Bounded asynchronous queue (`System.Threading.Channels`) |
| Tests | xUnit |
| Repository language | English (code, issues, commits, documentation, UI) |
| Source control | GitHub Flow: `main` + short-lived branches |

The core logic must **not** depend on Avalonia. This keeps file processing independently testable and leaves room for additional platforms.

## 3. MVP scope

### Included

- Choose and monitor a Downloads folder.
- Recognize when downloads are ready to process; avoid acting on partially written files.
- Categorize files into subfolders **within Downloads** (Documents, Images, Videos, Archives, Installers, Other).
- Handle duplicate filenames and failed file operations safely.
- Track files and decisions in a local database.
- Apply configurable age thresholds and move eligible items into **Expiring Files** for review.
- Offer **Keep**, **Snooze**, **Move**, and **Recycle** actions.
- Show recent activity, pending reviews, errors, and basic settings.
- Provide a dry-run preview before applying rules to existing files.
- Support undo for eligible moves, with clear warnings when an action cannot be reversed.

### Deliberately deferred

- AI-based categorization and PDF-content analysis.
- Image compression, cloud sync, and browser extensions.
- Arbitrary scripting and complex user-authored rules.
- macOS/Linux release support.
- Permanent automatic deletion.

## 4. Safety rules (non-negotiable)

1. **Never delete permanently by default.** Send user-requested deletions to the system recycle bin.
2. **Never assume a `Created` event means a download is finished.** Check stability and retry transient failures.
3. **Treat file watcher notifications as hints, not guaranteed complete history.** Rescan the managed directory on startup and periodically.
4. **Avoid loops.** Ignore or explicitly account for Dowlume-managed destinations.
5. **Prevent concurrent operations on the same file.** Queue and deduplicate tasks.
6. **Keep an operation journal.** Record original path, new path, operation, status, timestamp and any error.
7. **Validate paths.** Do not follow unexpected symlinks or move files outside configured boundaries without explicit user action.
8. **Make recovery possible.** Failed and interrupted operations must be visible and retryable.
9. **Use clear lifecycle semantics.** Base age on the time Dowlume first tracks a file; store Keep/Snooze decisions persistently.
10. **Protect user data in tests.** Test against disposable temporary folders, never the real Downloads directory.

## 5. Proposed solution layout

```text
Dowlume/
├── src/
│   ├── Dowlume.App/             # Avalonia views and view models
│   ├── Dowlume.Core/            # Models, classification and lifecycle rules
│   ├── Dowlume.Monitoring/      # Watcher, stability checks and scan scheduling
│   └── Dowlume.Infrastructure/  # SQLite, filesystem operations and logging
├── tests/
│   ├── Dowlume.Core.Tests/
│   └── Dowlume.Integration.Tests/
├── docs/
├── README.md
├── ROADMAP.md
├── GETTING_STARTED.md
├── .gitignore
└── Dowlume.slnx
```

Create these projects **incrementally**, not all at once. Prefer a modular monolith to microservices.

## 6. Git workflow

Use **one permanent branch: `main`**. Every change starts in a short-lived branch created from an up-to-date `main`, then reaches `main` through a pull request after review and passing checks.

Branch naming examples:

- `chore/project-setup`
- `feature/file-monitoring`
- `feature/download-completion`
- `feature/file-categorization`
- `feature/file-lifecycle`
- `feature/review-center`
- `fix/duplicate-events`
- `docs/update-architecture`

Use Conventional Commits, such as `feat(monitoring): queue newly detected files` or `test(core): cover category matching`.

Basic workflow:

```bash
git switch main
git pull origin main
git switch -c chore/project-setup
# Make changes and run tests
git add .
git commit -m "chore: initialize .NET solution"
git push -u origin chore/project-setup
# Open a pull request into main, merge after checks, then delete the branch
```

For a solo project, avoid introducing `develop` until there is a concrete need for an integration branch.

## 7. Implementation roadmap

### Milestone 0 — Foundation

- [ ] Create the GitHub repository `dowlume` and set its description.
- [ ] Add `README.md`, `ROADMAP.md`, `GETTING_STARTED.md`, `.gitignore` and a license decision.
- [ ] Install the supported .NET SDK and Avalonia templates.
- [ ] Create a solution with the Avalonia app, core library and xUnit test project.
- [ ] Reference the core library from the app without placing business logic in UI code.
- [ ] Confirm that `dotnet build` and `dotnet test` pass.
- [ ] Add GitHub Actions to build and test pull requests.

**Done when:** A clean checkout builds, tests pass, and the app opens a minimal window.

### Milestone 1 — Safe detection

- [ ] Add a user-configurable Downloads path.
- [ ] Observe changes with `FileSystemWatcher`.
- [ ] Implement a bounded work queue and duplicate-event handling.
- [ ] Detect stable files; exclude browser temporary downloads.
- [ ] Scan on startup to recover missed events.
- [ ] Test rapid downloads, incomplete files, renames and watcher restart.

**Done when:** New, completed files appear reliably in a processing queue without being moved.

### Milestone 2 — Classification

- [ ] Build categories and extension-based matching rules.
- [ ] Create category subfolders inside Downloads.
- [ ] Implement safe moves and filename collision handling.
- [ ] Add a dry-run preview and an operation journal.
- [ ] Test permissions failures, cancellation and moving across filesystems.

**Done when:** Sample downloads are classified correctly, and failures never silently lose files.

### Milestone 3 — File lifecycle

- [ ] Persist first-seen time, category, current path and lifecycle status.
- [ ] Implement configurable age thresholds per category.
- [ ] Add the `Expiring Files` review area.
- [ ] Implement Keep, Snooze, Move and Recycle with explicit confirmation where appropriate.
- [ ] Make reprocessing idempotent: completed decisions do not cause repeated moves.

**Done when:** Old files are flagged for review without automatic permanent deletion.

### Milestone 4 — Usable desktop UI

- [ ] Create a dashboard showing categorized and review-pending files.
- [ ] Add rule settings and folder selection.
- [ ] Build an activity/history view and error states.
- [ ] Show a dry-run result before organizing existing files.
- [ ] Add undo for supported operations.

**Done when:** A nontechnical user can configure, operate and understand the app without terminal commands.

### Milestone 5 — Hardening and first release

- [ ] Expand unit and integration tests with temporary directories.
- [ ] Test startup recovery, crashes during moves, locked files and high-volume batches.
- [ ] Add structured logs and avoid leaking personal file paths in shared diagnostics.
- [ ] Package a Windows installer and document usage/limitations.
- [ ] Tag the first pre-release (`v0.1.0`).

**Done when:** A tester can install the app, organize a test Downloads folder, and recover from ordinary failures.

## 8. The very first development session

**Goal:** a running Avalonia shell plus a tested core library — *no file movement yet*.

1. Create the repository and the branch `chore/project-setup`.
2. Initialize the .NET solution and add the app/core/test projects.
3. Create a minimal window titled **Dowlume** showing the project's current status.
4. Define initial core types (`FileCategory`, `TrackedFile`, `LifecycleStatus`) without implementing processing.
5. Add one basic xUnit test for category matching.
6. Run the build and tests; open the first pull request.

Do not add a background service, database, or complex settings system in this first session. Keep the first pull request small and reviewable.

## 9. Initial acceptance criteria

Before any real Downloads folder is used, the app must demonstrate:

- Reliable behavior in a disposable test directory.
- No destructive default operations.
- Clear handling of file collisions and errors.
- Repeatable test results and working CI.
- A visible user decision before cleanup actions.

---

**Next task:** Complete Milestone 0 in `chore/project-setup`, then start `feature/file-monitoring`.
