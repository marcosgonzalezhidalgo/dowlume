# Dowlume 0.1.0 — Windows

**Release draft: not approved or published as stable.** Clean-machine acceptance, native Recycle Bin failure cases, and independent first-use/release review remain pending. The artifacts are for acceptance testing until those checks pass.

Dowlume organizes completed downloads into configurable categories and moves aging files into Expiring Files for your decision. Preview and confirmation precede organization; monitoring starts paused after every launch.

- Keep, Snooze, Move, and Windows Recycle Bin actions for files in review.
- Searchable Activity history with paths, status filters, protected undo, and interrupted-operation recovery.
- Local SQLite storage, no account or telemetry, and no automatic permanent deletion.
- A self-contained Windows x64 portable ZIP and per-user installer, including an offline quick-start guide.
- Upgrade and uninstall require the application to exit first. Uninstall retains user files and the local profile.
- The notification-area icon restores hidden or minimized windows. Windows sign-in startup is opt-in; monitoring still starts paused.
- Windows moves use verified file handles to resist source replacement and destination collisions. Recycle operations recheck reviewed content before the shell acts.

## Downloads

- `Dowlume-0.1.0-win-x64-setup.exe`: per-user installer.
- `Dowlume-0.1.0-win-x64.zip`: extract the whole archive and run `Dowlume.App.exe`.
- `Dowlume-0.1.0-windows-acceptance.zip`: package smoke-test script and manual acceptance procedure.
- `BUILD-INFO.json` and `SHA256SUMS.txt`: exact source revision and artifact checksums.

No separate .NET runtime is required. The initial artifacts are unsigned. Check their SHA-256 values and download only from this repository's release page. macOS and Linux packages are not included.

## Limits and feedback

Use a fully local folder and start with disposable samples. Network shares, links, junctions, and cloud placeholders are unsupported. Cross-volume moves can leave copies requiring manual inspection after interruption. Changed content is not accepted automatically, external renames are not followed, and Recycle actions are restored through Windows rather than in-app undo. Keep backups of valuable files.

See the [user guide](https://github.com/marcosgonzalezhidalgo/dowlume/blob/main/docs/USER_GUIDE.md) and [acceptance checklist](https://github.com/marcosgonzalezhidalgo/dowlume/blob/main/docs/RELEASE_CHECKLIST.md). Report problems with the Windows build, application version, exact steps, and expected/actual result. Do not attach personal files, the database, or unredacted paths.
