# Platform support

Windows x64 remains the supported development and packaging target. macOS and Linux versions are deferred; no packages or acceptance results are claimed for those systems.

## Current assessment

The application uses Avalonia and separates domain rules from desktop integration. That structure provides a starting point for ports, but compiling the projects for another runtime would not establish safe file handling there.

| Area | Current implementation | Work required for another platform |
| --- | --- | --- |
| Recycling | [WindowsRecycleBin](../Dowlume/Dowlume.Infrastructure/WindowsRecycleBin.cs) uses Windows COM and a recycle-only progress sink | Native Trash adapter with verified recoverability and no permanent-delete fallback; refusal, cancellation, unavailable Trash, and interruption tests |
| File manager | [DesktopDialogs](../Dowlume/Dowlume.App/Services/DesktopDialogs.cs) selects files in Explorer | Native reveal integration with argument-safe process invocation |
| Folder discovery | [DownloadsFolder](../Dowlume/Dowlume.Infrastructure/DownloadsFolder.cs) uses the Windows known folder, with a simple home-directory fallback | Platform folder discovery, including configured Downloads locations |
| Filesystem behavior | [PathSafety](../Dowlume/Dowlume.Infrastructure/PathSafety.cs), [SafeFileMover](../Dowlume/Dowlume.Infrastructure/SafeFileMover.cs), and monitoring are validated on Windows | Audit volume case sensitivity, path equality, protected folders, links, mount boundaries, writer locks, collisions, and cross-volume recovery on each target |
| Desktop lifecycle | Window, tray, profile lock, and shutdown have Windows evidence | Verify tray availability, reopen/exit, single-instance behavior, folder dialogs, and profile locations on each target |
| Distribution | [Release script](../scripts/Build-Release.ps1) publishes Windows x64 and builds an Inno Setup installer | Native packaging, launch permissions, upgrade/uninstall checks, and platform signing decisions |

## Acceptance sequence for a port

1. Implement the native adapters behind the existing contracts and select them in the composition root.
2. Audit path comparisons and file-operation assumptions before enabling monitoring on that system.
3. Add native CI jobs for domain, persistence, monitoring, recovery, and headless desktop tests. Keep shell tests explicit and confined to disposable files.
4. Validate real desktop behavior and Trash recovery on each supported OS and architecture, including failures and interrupted cross-volume moves.
5. Build and test native packages, document remaining limits, and update the support table only when evidence is available.

The current [Windows release gates](RELEASE_CHECKLIST.md) remain open independently of this portability work. The [roadmap](../ROADMAP.md) records the ports as future work without a promised date.
