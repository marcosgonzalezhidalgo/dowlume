# Dowlume

[![Build and test](https://github.com/marcosgonzalezhidalgo/dowlume/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/marcosgonzalezhidalgo/dowlume/actions/workflows/build.yml?query=branch%3Amain)

**Your downloads, under control.**

Dowlume is a local-first Windows desktop application that organizes completed downloads, moves aging files into a review area, and leaves the final decision with you.

**Status:** The Windows MVP is available on `main` as a 0.1.0 release candidate. See [validation](docs/VALIDATION.md) for completed checks and the [release checklist](docs/RELEASE_CHECKLIST.md) for publication gates.

[User guide](docs/USER_GUIDE.md) · [Developer setup](GETTING_STARTED.md) · [Roadmap](ROADMAP.md) · [Contributing](CONTRIBUTING.md)

## What it does

- Preview existing files before consenting to organization and monitoring.
- Detect completed downloads with a watcher, bounded queue, readiness checks, and periodic reconciliation.
- Sort files into configurable Documents, Images, Videos, Archives, Installers, and Other folders.
- Handle duplicate names without overwriting existing files.
- Track first-seen time, category, decisions, and operations in a local SQLite database.
- Move aging files into **Expiring Files**, where you can Keep, Snooze, Move, or Recycle them.
- Undo eligible moves and reconcile interrupted operations without guessing which copy to delete.
- Show a dashboard, review queue, recent activity, errors, settings, and a Windows tray menu.

There is no automatic permanent deletion, online account, cloud sync, or telemetry. Monitoring starts paused on launch. Existing subfolders are not imported automatically.

## Run from source

Install the .NET 10.0.4xx SDK on Windows, then:

```powershell
dotnet restore Dowlume/Dowlume.slnx
dotnet build Dowlume/Dowlume.slnx -c Release
dotnet test Dowlume/Dowlume.slnx -c Release --filter "Category!=WindowsShell"
dotnet run --project Dowlume/Dowlume.App -c Release
```

Choose a disposable sample folder in Settings first, then use Preview. The [user guide](docs/USER_GUIDE.md) explains the complete workflow, recovery, and current limits.

## Packages

The release script produces a self-contained Windows x64 ZIP and, when Inno Setup is available, a per-user installer. See [packaging instructions](docs/RELEASE_CHECKLIST.md). Locally generated files are in `artifacts/` and are not committed. A published installer is pending completion of the release checklist.

## Project

| Project | Responsibility |
| --- | --- |
| `Dowlume.Core` | Domain models, rules, validation, contracts |
| `Dowlume.Monitoring` | Watcher, queue, readiness, reconciliation scheduling |
| `Dowlume.Infrastructure` | SQLite, filesystem safety, recovery, Windows integration |
| `Dowlume.App` | Avalonia MVVM desktop interface |
| `Dowlume.Tests` | Unit, integration, shell, and headless desktop tests |

`main` is the default branch and contains the integrated application and documentation. Read [Architecture](docs/ARCHITECTURE.md) for the design and [Git history and branch policy](docs/BRANCHES.md) for the merged implementation PRs and future workflow.

## License

[MIT](LICENSE). Third-party dependencies retain their respective licenses.
