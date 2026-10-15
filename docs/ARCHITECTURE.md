# Architecture and safety boundaries

## Project responsibilities

| Project | Responsibility |
| --- | --- |
| Dowlume.Core | Immutable records, category validation, classification, lifecycle rules, service contracts. No UI or SQLite dependency. |
| Dowlume.Monitoring | FileSystemWatcher, bounded channel, deduplication, readiness observations, periodic discovery. Depends only on Core. |
| Dowlume.Infrastructure | SQLite persistence, path checks, verified moves, operation orchestration, Windows shell adapters, diagnostics. |
| Dowlume.App | Avalonia views, view models, dialogs, tray behavior, constructor-based composition and shutdown. |
| Dowlume.Tests | Unit, disposable-filesystem integration, native shell, and headless desktop workflow tests. |

The existing solution layout is retained instead of moving every project into new `src` and `tests` directories. This avoids unrelated churn while keeping the documented separation of concerns.

## Processing

```text
Watcher hints + startup/15-second scan
    -> deduplicated bounded channel (256 paths)
    -> one consumer
    -> readiness and path checks
    -> category rule
    -> persist Pending journal entry
    -> verified no-overwrite move
    -> transaction: tracked state + Completed journal entry
```

Watcher events are hints. Queue overflow is recovered by later scans. Managed destinations are not watched recursively. The readiness policy requires a three-second write-age margin, stable length and last-write time across two observations, and a successful exclusive open. Cancellation is checked before journal creation. Once journaled, an operation finishes or records its failure before shutdown completes.

`DownloadOrganizer` serializes file mutations through one semaphore. This covers monitoring, lifecycle checks, user actions, recovery, and undo. SQLite connections are short-lived and use parameterized statements, WAL, full synchronization, and a busy timeout. Schema version 1 stores versioned settings, tracked records, and operations as JSON payloads in separate tables. A database created by a newer schema is refused.

The application takes an exclusive profile lock before opening its database. It does not auto-start monitoring after a restart. All dependencies are supplied through constructors in the application composition root; a service locator or DI container is unnecessary for this scope.

## Lifecycle

```text
Untracked -> Organized -> InReview
                             |-> Kept (no further aging)
                             |-> Snoozed -> InReview after 7 days
                             |-> Moved (outside application management)
                             |-> Recycled (restore through Windows)
Managed file disappears -> Missing
```

First-seen time is preserved throughout decisions and undo. Keep and Snooze are persisted, not inferred from filesystem timestamps. Existing untracked category contents are left alone.

## Operation journal and recovery

Each entry contains an ID, kind, status, UTC timestamp, source/destination, before/after file snapshots, optional undo link, and a user-safe error. State and completion are committed in one SQLite transaction after the filesystem action. This is not a transaction across SQLite and NTFS: Pending entries bridge that gap.

Recovery verifies SHA-256 before completing a move that reached its destination. Conflicting or missing copies remain NeedsAttention and block further automatic operations for that file/path. Recycle disappearance cannot be proved from the original path alone; its ambiguous recovery requires restoring the item through Windows. Failure handling continues reconciliation for unrelated files.

Undo verifies that the selected operation is the latest completed action on the file, that no completed undo references it, and that the current tracked record still matches. Restoring to an occupied path fails instead of overwriting. Undoing an initial organization creates a Kept record at the original path to prevent a watcher loop. Undoing a review move snoozes the restored file for seven days to prevent an immediate lifecycle loop.

## Filesystem safety

- Compare normalized paths with a directory separator boundary; prefix matching alone is insufficient.
- Reject reparse points in every existing ancestor, including destination paths.
- Validate category names, reserved Windows names, extension uniqueness, and bounded review periods.
- Recheck paths before moving; never pass `overwrite: true` for user-file moves.
- Hold a read handle that denies new writers during hash verification and the move/recycle action.
- Use Windows `IFileOperation` with recycle-only flags and a progress sink that aborts if the item is not marked recyclable.
- Never empty the user's Recycle Bin or permanently delete managed files.

Path checks reduce ordinary link and rename hazards but are not a security boundary against a hostile same-user process. Cross-volume moves are non-atomic and may require manual reconciliation. Hashing is streamed, so memory use does not grow with file size; processing time does grow with it. The current store reads record collections into memory and is intended for personal download folders, not millions of files.

## Diagnostics and references

The bounded diagnostic log rotates at approximately 1 MB and excludes exception messages and stack traces because they can reveal paths. The private journal intentionally retains paths for recovery.

Primary API references used by the implementation:

- [FileSystemWatcher and buffer overflow behavior](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0).
- [File.Move and cross-volume behavior](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-10.0).
- [Windows file-operation flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags), [pre-delete cancellation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-predeleteitem), and [transfer flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-_transfer_source_flags).
- [Avalonia storage dialogs](https://docs.avaloniaui.net/docs/services/storage/storage-provider) and [headless rendering](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform).
