# Windows release acceptance

Use this procedure for the exact installer and ZIP proposed for `v0.1.0`. Record their SHA-256 values, source commit from `BUILD-INFO.json`, Windows build, tester, date, and observed results. An unchecked item is not a pass. Do not use personal Downloads or change Recycle Bin settings on a daily-use computer.

## Prepare a clean environment

Use a fresh Windows 11 x64 VM or spare machine, a standard user account, and no separately installed .NET runtime. Record how the image was created. A development PC or a GitHub runner with the SDK installed does not satisfy this check.

Download the proposed installer, portable ZIP, `BUILD-INFO.json`, and `SHA256SUMS.txt`. Compare each file with `Get-FileHash -Algorithm SHA256`. The initial candidate is unsigned; checksums identify the reviewed artifacts but do not replace trusting their source. Do not publish a package built with `DirtyWorkingTree: true`.

## Automated package smoke test

The acceptance kit contains `Test-WindowsPackage.ps1`, which runs on Windows PowerShell without installing .NET. From the directory containing the downloaded artifacts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-WindowsPackage.ps1 -Installer .\Dowlume-0.1.0-win-x64-setup.exe -Archive .\Dowlume-0.1.0-win-x64.zip -ReportPath .\package-result.json -RequireNoSharedDotNet
```

The script refuses an existing registered Dowlume installation. It creates a unique temporary installation, application profile, and sample file; checks portable launch/exit, install/reinstall, duplicate-instance refusal, running-app upgrade/uninstall refusal, and profile/file retention after uninstall. Reports and fixtures remain in the printed temporary directory for inspection. It records detected runtimes but does not independently prove that the OS image was clean.

To test an upgrade from the earlier candidate, add `-PreviousInstaller <path-to-previous-installer>`. Without that argument, the upgrade check is a reinstall of the same package. Preserve both installer hashes in the report. If a check fails, inspect its retained logs before rerunning; the script intentionally refuses to overwrite a registered installation left by a failed test.

## First use with a person

After the smoke test, install normally with the wizard and a standard account. Ask someone unfamiliar with the implementation to perform these steps without developer assistance:

1. Open the Start menu shortcut and the included quick-start guide.
2. Create a disposable folder with copies of a document, image, archive, and an unfinished `.crdownload` file. Save that folder in Settings.
3. Refresh Preview. Confirm that files remain at their original locations, only completed files appear, and proposed destinations are understandable.
4. Select Organize and monitor, then cancel. Confirm that nothing moved. Repeat and confirm; verify the category folders and file contents.
5. Add another completed sample, wait for organization, then Pause. Confirm that a subsequent sample is left in place.
6. Use Hide to tray, reopen from the tray icon, and exit from the tray menu. Confirm the process has exited. Relaunch and verify that monitoring remains paused.
7. Find the sample in Activity, inspect its paths, and undo an eligible operation. Check that content is preserved and an occupied original path is never overwritten.
8. Close the app, upgrade/reinstall through the wizard, and reopen it. Confirm the saved configuration and history remain. Close and uninstall; confirm the sample files and profile remain.

Record unclear wording, errors, unexpected windows, display scaling, and keyboard navigation problems. Attach only redacted screenshots; the journal and profile contain private paths.

## Native file-safety acceptance

- **Cross-volume move and interruption:** on two separate local volumes, use the harness below. Record the physical disk/volume arrangement. Verify a completed move and undo, then terminate only the isolated worker during a copy. Recovery must preserve the intact source and partial destination, mark NeedsAttention, and block another move until resolved. A source-only or fully completed copy is an inconclusive interruption test.
- **Disabled Recycle Bin:** in the disposable VM/account only, disable recycling for the test volume, then attempt to recycle a disposable review file through the app. The action must fail and the source content must remain. Restore the test setting afterward.
- **Insufficient Recycle Bin capacity:** in that same isolated environment, set a small bin capacity and test a disposable file exceeding it, then a bin near capacity. The application must not silently permanently delete the source or discard unrelated bin contents. Record the shell behavior and source hash. Never fill a personal disk or empty an existing personal bin for this test.
- **Ordinary recycling and restore:** recycle a uniquely named sample, locate it in Windows Recycle Bin, restore it through Windows, and compare its content.

Developer cross-volume harness (requires the repository SDK; it is not part of clean-machine testing):

```powershell
dotnet run --project scripts/FileOperationAcceptance -c Release -- verify E:\Dowlume.Acceptance artifacts/acceptance/cross-volume.json
```

Choose a writable folder on a second fixed local volume. The harness writes only uniquely named fixture directories, moves a 64 MiB sample and undoes it, then interrupts a separate 512 MiB copy. It retains both fixture directories and reports their paths. It does not simulate power loss or modify Recycle Bin settings.

## Publication handoff

Update the release checklist with evidence tied to the artifact hashes. Resolve failures, obtain independent acceptance review, and repeat affected checks after changes. Only then publish the prepared GitHub draft as stable `v0.1.0`. Share the release with a small group of testers who can access the repository and collect reproducible bug reports before starting Linux/macOS work or new features.
