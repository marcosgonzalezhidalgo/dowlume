# Windows security validation

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Security.ps1` from the repository. The script creates a fresh ignored report directory and fails on incomplete dependency results, vulnerable direct/transitive packages, analyzer warnings, or detected secrets. Gitleaks 8.30.1 is downloaded from its official release and verified against a pinned SHA-256 before execution. Reports redact secret values; CI uploads only report files, not source snapshots or private application profiles.

All .NET security analyzers run during normal builds with warnings treated as errors. The dedicated security workflow additionally scans dependencies and all fetched Git history, plus a snapshot of tracked and unignored source files. Local helper projects are built with the same analyzers. NuGet data and scanner rules can change; rerun before publication. Passing scans do not prove that software is free of vulnerabilities.

## Reviewed boundaries

- SQL statements are selected from a private enum and fixed literals; record values and history filters remain parameters. No local file name or search text becomes executable SQL.
- Explorer uses its absolute Windows path and structured arguments. Native DLL imports explicitly search System32. Startup registration uses a quoted absolute application path in the current user's Run key and starts disabled.
- Windows moves use the verified source handle, reject existing destinations, and hold ancestor directory handles against renames. Cross-volume copies preserve alternate streams, verify the main-stream hash before source removal, and preserve ambiguous copies for recovery.
- Organizer actions are serialized. Regression coverage includes overlapping review decisions, changed previews, interrupted journals, concurrent source replacement, and late destination collisions.
- Recycling preserves the reviewed hash across the asynchronous shell handoff and verifies it again in the pre-delete callback. A negative result, a missing recycled destination, or an unconfirmed shell outcome does not complete the journal entry. There is no permanent-delete fallback.

## Remaining acceptance and limits

Use the [Windows acceptance procedure](WINDOWS_ACCEPTANCE.md) for clean installation, manual organization/undo, tray overflow, sign-in startup, upgrades, uninstall, and disabled/undersized/full Recycle Bin tests. All fixtures must be fictional and all policy changes confined to a disposable guest.

On 2026-10-16, Hyper-V's service was running on the host, but `Get-VM` returned an authorization error in the agent session. No guest was inspected or modified, and no VM/manual acceptance result is claimed. The local native recycle test covers ordinary recycling and changed-content refusal, not disabled/full policy behavior.

Filesystem checks are not a security boundary against another hostile process with the same user's privileges. In particular, Windows shell recycling still requires delete sharing and a path-based handoff; its final callback-to-shell interval is not atomic. Hashes cover the main content stream. Process-interruption tests do not simulate power loss or damaged storage. These limits and the outstanding native/manual checks prevent closing stable-release acceptance merely because scanners pass.
