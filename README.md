# Dowlume

**Your downloads, under control.**

Dowlume is a planned, local-first desktop application that helps you keep your Downloads folder organized without taking control away from you. It sorts completed downloads into manageable categories, identifies files that may no longer be needed, and provides a safe review workflow before you decide what to keep, move, or discard.

> **Project status:** Early planning. The features described below are goals, not yet implemented.

## Why Dowlume?

Downloads folders accumulate installers, documents, pictures, archives, and temporary files. Traditional cleanup tools can require complex rules or make irreversible changes. Dowlume aims to provide a simpler alternative: organize downloads inside their existing folder, highlight aging files, and let users make the final decision.

## Planned features

### First release (MVP)

- **Automatic organization:** Sort completed downloads into subfolders such as `Documents`, `Images`, `Videos`, `Installers`, `Archives`, and `Other`.
- **Download monitoring:** Detect changes in the selected Downloads directory, while avoiding processing files that are still being written.
- **Review queue:** Move eligible older downloads to an `Expiring Files` review folder, with configurable age thresholds per category.
- **Keep, move, or discard:** Mark files to keep, move them to a chosen destination, postpone review, or send them to the operating system's Trash / Recycle Bin.
- **Safe defaults:** Never permanently delete files automatically; avoid overwriting files with the same name.
- **Activity history:** Record file operations to explain what happened and support undo where possible.
- **Simple settings:** Configure monitored folders, sorting categories, and review periods.

### Later possibilities

- Dry-run previews before applying rules to existing files.
- Advanced rules based on filenames, sizes, metadata, and document contents.
- Duplicate detection using file hashes.
- Storage usage insights and cleanup suggestions.
- Optional virtual categorization without physically moving files.
- Cross-platform support and localization.

## Intended workflow

```text
New download
     |
     v
Detect completion
     |
     v
Classify by file type
     |
     v
Downloads/<Category>/
     |
     v
Age threshold reached?
     | No               | Yes
     |                  v
     |          Expiring Files/
     |                  |
     |          User reviews file
     |                  |
     |          Keep / Move / Trash
     v
Remain in category
```

The review folder is a **holding area, not a deletion queue**. Files stay there until the user takes action, unless they explicitly enable a different policy in a future version.

## Example folder structure

```text
Downloads/
├── Documents/
├── Images/
├── Videos/
├── Installers/
├── Archives/
├── Other/
└── Expiring Files/
```

Folder names, category mappings, and aging thresholds are planned to be configurable. Dowlume should also track the original category and path of files moved to review.

## Proposed tech stack

| Area | Technology |
| --- | --- |
| Language | C# / .NET |
| Desktop UI | Avalonia UI |
| Local database | SQLite |
| Filesystem monitoring | `FileSystemWatcher` plus periodic reconciliation |
| Background processing | `async`/`await` and `System.Threading.Channels` |
| Testing | xUnit |
| Initial platform | Windows |

## Engineering principles

1. **Protect user data.** No silent permanent deletion, accidental overwrite, or uncontrolled file moves.
2. **Handle real-world filesystem behavior.** Expect duplicate or missed events, incomplete downloads, locked files, restarts, and rename conflicts.
3. **Keep operations traceable.** Store enough metadata to explain actions and support safe recovery.
4. **Stay lightweight.** Monitor and process files in the background without unnecessary CPU usage.
5. **Separate concerns.** Keep the file monitor, classification engine, lifecycle policies, persistence, and UI independently testable.

## Development status

Dowlume is in the design and planning stage. There are currently no published binaries or installation instructions.

See [ROADMAP.md](ROADMAP.md) for implementation milestones and release criteria.

## Contributing

The contribution process will be defined once the initial project structure and development guidelines are in place. Feedback and design discussions are welcome when the repository opens for collaboration.

## License

A license has not yet been selected. Until one is added, do not assume that the source code is licensed for reuse or redistribution.
