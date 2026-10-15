# Using Dowlume

Dowlume 0.1.0 is a Windows desktop download organizer. Start with a disposable folder containing copies of sample files before choosing your real Downloads folder.

## Install and start

Run `Dowlume-0.1.0-win-x64-setup.exe`. Installation is per user and does not require administrator privileges. Alternatively, extract the complete portable ZIP and run `Dowlume.App.exe`; do not move the executable out of its accompanying files. The .NET runtime is included.

The initial packages are unsigned. Only use a package from a source you trust and compare its SHA-256 checksum with the accompanying `SHA256SUMS.txt`.

## First use

1. Open **Settings** and choose an existing local Downloads folder. The suggested path comes from the Windows Downloads known folder.
2. Adjust category folder names, extension mappings, excluded extensions, and review periods. Save settings.
3. Open **Preview** and select **Refresh preview**. This step does not move files or create category folders.
4. Review the proposed destinations, then select **Organize and monitor** and confirm. Consent covers previewed files, future arrivals, and unfinished files when they become ready.
5. Check **Overview** and **Activity** to see what happened.

Only files directly inside the selected folder are discovered. Existing subfolders are not imported or rearranged. Category folders and `Expiring Files` are created when needed. Name collisions receive a numeric suffix; existing files are never overwritten.

Monitoring starts paused on every launch. Preview and confirm again to resume. **Pause** stops the watcher and waits for an active operation. **Hide to tray** keeps the application running; use the tray menu to reopen it or exit. Closing the main window stops monitoring.

## Review files

A file's age starts when Dowlume first organizes it, not from its downloaded or modified timestamp. When its category's review period expires, it moves to `Expiring Files`.

Select a file in **Review**:

- **Keep:** return it to its category and exclude it from future age reviews.
- **Snooze 7 days:** return it to its category and review it again after seven days.
- **Move to:** choose a destination and confirm; Dowlume stops managing that file.
- **Recycle:** confirm a move to Windows Recycle Bin. Windows must report the file as recyclable; otherwise the action fails. There is no permanent-delete fallback.
- **Show in Explorer:** inspect the selected file without executing it.

Review is a holding area. There is no automatic deletion deadline.

## Undo and recovery

Select a completed operation in **Activity** and choose **Undo selected action**. Undo requires unchanged file content, an available original location, and no later completed action on the same file. Undoing initial organization returns the file to the root and marks it Kept, preventing immediate reorganization. Undoing an age-based review move returns the file to its category and snoozes it for seven days, so the next scan does not immediately move it back. Recycle actions are restored through Windows Recycle Bin, not the app.

The operation journal is committed before moving a file. After interruption, **Retry interrupted operations** reconciles the recorded source and destination:

| Filesystem state | Recovery behavior |
| --- | --- |
| Original source remains and destination is absent | Mark failed; retry the original action. |
| Source is absent and destination has matching content | Complete the interrupted journal entry. |
| Both paths exist, neither exists, or content differs | Mark NeedsAttention and leave files untouched. |

For NeedsAttention, copy the paths from Activity, inspect them in Explorer, and preserve any content you need. Once the filesystem has one unambiguous matching copy at the recorded source or destination, retry recovery. If recycling was interrupted after the file disappeared, restore it from Windows Recycle Bin to the original location before retrying recovery. Dowlume does not guess whether a missing file was recycled or removed elsewhere.

If a tracked file was edited, automatic moves refuse to act on the old content hash. Move the edited file to a location outside the managed root if you want to manage it yourself. If you intentionally want to import it again, return it to the Downloads root under a new name and preview it. External renames are not followed automatically.

## Settings and data

- Settings changes affect future categorization and current age checks. Renaming a category in settings does not rename an existing folder.
- Only one monitored folder is active at a time. Changing folders retains the earlier folder's journal; returning to it restores the dashboard's tracked records.
- The local database and diagnostics live in `%LOCALAPPDATA%\Dowlume`.
- The database contains personal file paths and content hashes. Treat it as private. Diagnostic JSON lines contain event names and error types/codes, not file paths or exception messages.
- To back up the database, exit Dowlume and copy the whole local data directory, including any SQLite sidecar files.
- Uninstalling removes application binaries and shortcuts, not organized files or the database.
- Developers can use `Dowlume.App.exe --data-directory <absolute-path>` to isolate a test profile. Do not run separate profiles against the same folder.

## Current limits

Windows x64 is the release target. Network shares, drive roots, symbolic links, junctions, and cloud placeholder/reparse paths are rejected. Use a fully local folder. The application does not run as a service or start with Windows.

Completion detection checks file age, two stable size/timestamp observations, and an exclusive-open probe. It is conservative but cannot prove that a program will never reopen a file later. Browser partial-download extensions are always excluded. Avoid downloading directly into managed category folders.

Cross-volume moves use the operating system's copy-and-delete behavior and are not atomic. An interruption can leave two copies requiring manual inspection; recovery never deletes either copy automatically. Large-file hashing and cross-volume moves may delay pause or exit while the current operation finishes.

The app guards against ordinary filesystem changes, not a malicious process with the same user privileges deliberately replacing paths during operations. Keep backups of valuable files.
