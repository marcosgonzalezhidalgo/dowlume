# Getting started with Dowlume

## Requirements

- Windows x64 for the supported desktop application and native Recycle Bin integration.
- .NET SDK 10.0.400 or a later patch in the same feature band (`global.json`).
- Inno Setup 6 only when building the installer.

The repository uses C#, Avalonia MVVM, SQLite, System.Threading.Channels, and xUnit. All project-facing text is English.

## Build and test

```powershell
dotnet restore Dowlume/Dowlume.slnx
dotnet build Dowlume/Dowlume.slnx -c Release --no-restore
dotnet test Dowlume/Dowlume.slnx -c Release --no-build --filter "Category!=WindowsShell"
dotnet run --project Dowlume/Dowlume.App -c Release
```

The native shell test is separate because it places one uniquely named disposable file into Windows Recycle Bin:

```powershell
dotnet test Dowlume/Dowlume.slnx -c Release --filter "Category=WindowsShell"
```

Use a disposable folder for initial UI testing. The real Downloads directory is never used by automated tests. To isolate application state as well, pass `-- --data-directory C:\path\to\test-profile` to `dotnet run`.

## First workflow

1. Open Settings and choose the disposable Downloads folder.
2. Save category names, extension mappings, review days, and exclusions.
3. Refresh Preview and inspect destinations. No file has moved yet.
4. Select Organize and monitor, then confirm. Future and unfinished downloads are included in this consent.
5. Use Overview to see tracked files, Review for decisions, and Activity for journal details and undo.
6. Pause before changing settings. Close to exit, or hide to tray to keep monitoring.

For all behavior and recovery procedures, see the [user guide](docs/USER_GUIDE.md).

## Safety contract

- Never permanently delete a user file through Dowlume.
- Never overwrite an existing destination.
- Do not equate a Created event with download completion.
- Recover missed events through periodic scans.
- Serialize mutations and journal before moving.
- Reject unexpected links and validate folder boundaries.
- Persist first-seen time and Keep/Snooze decisions.
- Refuse ambiguous recovery and changed-content undo.
- Preserve the source on ordinary pre-move failures.
- Use disposable temporary folders in tests.

Read [Architecture](docs/ARCHITECTURE.md) for implementation details and practical filesystem limits.

## Git and release workflow

Use GitHub Flow: one permanent `main` branch, short-lived feature branches, Conventional Commits, and review plus passing checks before merge. The initial implementation has been merged into `main`; its [PR history and branch policy](docs/BRANCHES.md) explain the dependency order and ongoing workflow.

The implementation milestones are tracked in [ROADMAP.md](ROADMAP.md). Packaging and publication are separate: follow the [release checklist](docs/RELEASE_CHECKLIST.md), including clean-machine testing and hosted CI. Do not mark those complete solely because local tests pass.
