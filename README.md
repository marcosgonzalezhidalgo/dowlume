# Dowlume

[![Build and test](https://github.com/marcosgonzalezhidalgo/dowlume/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/marcosgonzalezhidalgo/dowlume/actions/workflows/build.yml?query=branch%3Amain)

**Your downloads, under control.**

Dowlume is a local-first Windows desktop application that organizes completed downloads, moves aging files into a review area, and leaves the final decision with you.

**Status:** Windows MVP implemented; **0.1.0 is a release candidate, not a published stable release**. Run from source while public packages are pending. See [validation](docs/VALIDATION.md) and the [release checklist](docs/RELEASE_CHECKLIST.md).

[User guide](docs/USER_GUIDE.md) · [Developer setup](GETTING_STARTED.md) · [Roadmap](ROADMAP.md) · [Contributing](CONTRIBUTING.md)

![Dowlume Overview showing six managed files, category counts, and two files awaiting review](docs/images/overview.png)

*The real Avalonia interface, rendered with fictional sample data. [Screenshot provenance and regeneration](docs/images/README.md).*

## Why Dowlume

Downloads folders accumulate useful documents, one-off installers, and files that are easy to forget. Dowlume was built to turn that clutter into a reviewable workflow: organize completed downloads, bring aging files back to your attention, and keep the final decision with you. The engineering focus is making file automation recoverable when downloads, disk operations, or the application are interrupted.

## At a glance

| Preview | Organize | Review | Recover |
| --- | --- | --- | --- |
| Inspect proposed destinations before consenting. | Classify completed downloads into six configurable categories. | Keep, Snooze, Move, or Recycle aging files in **Expiring Files**. | Inspect the activity journal, undo eligible moves, and resolve interrupted actions. |

![Dowlume Preview mapping sample PDFs, photos, videos, archives, and installers to category folders](docs/images/classification.png)

*Classification preview with sample data: inspect where each file will go before selecting **Organize and monitor**.*

There is no automatic permanent deletion, online account, cloud sync, or telemetry. Monitoring starts paused on launch. Existing subfolders are not imported automatically.

Activity now includes searchable history with action/status filters and pagination, so older operations remain accessible. See [finding an earlier action](docs/USER_GUIDE.md#find-an-earlier-action).

Windows x64 remains the current target. macOS and Linux versions are deferred pending native file-safety integrations and validation; see [platform support](docs/PLATFORM_SUPPORT.md).

## Technologies

**C# · .NET 10 · Avalonia (MVVM) · SQLite · xUnit**

The desktop UI uses Avalonia; SQLite stores settings, tracked files, and the operation journal. xUnit covers domain rules, filesystem safety, recovery, monitoring, and headless desktop workflows.

## Engineering challenges

- **Knowing when a download is ready:** watcher events trigger checks, not immediate moves. Stable size/timestamps, a write-age margin, and an exclusive-open probe work alongside periodic scans to handle unfinished downloads and missed events.
- **Protecting files during interruptions:** moves never overwrite a destination, content hashes detect unexpected changes, and ambiguous copies are left for inspection.
- **Keeping SQLite and disk consistent:** a Pending journal entry precedes each move; tracked state and completion are then committed together. Recovery checks the actual files before deciding what completed.

See [Architecture](docs/ARCHITECTURE.md#engineering-challenges) for the design, tradeoffs, and test coverage.

## Run from source

Install a **.NET SDK from the 10.0.4xx family** on Windows. [`global.json`](global.json) sets `10.0.400` with `rollForward: latestPatch`; CI currently uses `10.0.401` within that same feature band.

```powershell
dotnet restore Dowlume/Dowlume.slnx
dotnet run --project Dowlume/Dowlume.App -c Release
```

Choose a disposable sample folder in Settings first, then use Preview. See [developer setup](GETTING_STARTED.md) for build and test commands, or the [user guide](docs/USER_GUIDE.md) for the complete workflow and current limits.

## Packages

When a release package is available, download the installer or portable ZIP from [GitHub Releases](https://github.com/marcosgonzalezhidalgo/dowlume/releases). Until then, build and run from source. The release script can generate a self-contained Windows x64 ZIP and a per-user installer locally; see [packaging instructions](docs/RELEASE_CHECKLIST.md). Local packages in `artifacts/` are not published releases.

## Explore the project

| Document | What you will find |
| --- | --- |
| [Domain model](docs/requirements/DOMAIN_MODEL.md) | Conceptual vocabulary, UML structure, file lifecycles, processes, and business rules |
| [Architecture](docs/ARCHITECTURE.md) | Component diagram, processing flow, recovery, and safety boundaries |
| [Roadmap](ROADMAP.md) | Completed MVP, remaining release gates, and future ideas |
| [Validation](docs/VALIDATION.md) | Dated evidence and outstanding acceptance checks |
| [Contributing](CONTRIBUTING.md) | Contribution checks and [branch policy](docs/BRANCHES.md) |

## License

[MIT](LICENSE). Third-party dependencies retain their respective licenses.
