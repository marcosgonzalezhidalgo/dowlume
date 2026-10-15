# Getting started with Dowlume

## Requirements

- Windows x64 for the supported desktop application and native Recycle Bin integration.
- .NET SDK from the **10.0.4xx family**: [`global.json`](global.json) sets `10.0.400` with `rollForward: latestPatch`. CI uses `10.0.401`, a compatible patch in the same feature band.
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

## Next steps

Follow [First use](docs/USER_GUIDE.md#first-use) for Settings, Preview, consent, and review decisions. Read [Architecture](docs/ARCHITECTURE.md) before changing detection, file operations, or recovery; it defines the safety boundaries and practical filesystem limits.

## Git and release workflow

Use GitHub Flow: one permanent `main` branch, short-lived feature branches, Conventional Commits, and review plus passing checks before merge. See [Contributing](CONTRIBUTING.md) for validation and the [branch policy](docs/BRANCHES.md) for the workflow.

The implementation milestones are tracked in [ROADMAP.md](ROADMAP.md). Packaging and publication are separate: follow the [release checklist](docs/RELEASE_CHECKLIST.md), including clean-machine testing and hosted CI. Do not mark those complete solely because local tests pass.
