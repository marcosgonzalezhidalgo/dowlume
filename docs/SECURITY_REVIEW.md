# Windows security validation

**Known release blocker:** the clean guest's Recycle Bin capacity probes failed. An earlier recycled fixture disappeared when a new item was recycled into a near-full bin; the oversized case also failed its preservation assertion. Preserve the evidence and do not rerun the failed probes pending diagnosis. See the [investigation and proposed quarantine design](RECYCLE_CAPACITY_INVESTIGATION.md). The candidate must not be published as stable.

The 2026-10-17 review of two existing runs found no oversized source and no shell-visible bin item in the tester's final inventories. The harness has no file cleanup or alternate restore destination; its explicit incoming-item restore explains the near-capacity source's later reappearance. The complete raw bin inventory contained no payloads; all four surviving metadata records describe restored ordinary/near-capacity fixtures. Permanent deletion and capacity eviction remain the leading native explanations. A separate, hash-verified checkpoint copy was unlocked read-only using its existing BitLocker clear key, allowing first-run filesystem inspection without a recovery password or changes to the evidence VM. Access to the encrypted checkpoint does not establish recovery of the missing contents. Treat this as a failed recoverability invariant, regardless of the shell HRESULT. No architectural replacement is implemented yet.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Security.ps1` from the repository. The script creates a fresh ignored report directory and fails on incomplete dependency results, vulnerable direct/transitive packages, analyzer warnings, or detected secrets. Gitleaks 8.30.1 is downloaded from its official release and verified against a pinned SHA-256 before execution. Reports redact secret values; CI uploads only report files, not source snapshots or private application profiles.

All .NET security analyzers run during normal builds with warnings treated as errors. The dedicated security workflow additionally scans dependencies and all fetched Git history, plus a snapshot of tracked and unignored source files. Local helper projects are built with the same analyzers. NuGet data and scanner rules can change; rerun before publication. Passing scans do not prove that software is free of vulnerabilities.

## Reviewed boundaries

- SQL statements are selected from a private enum and fixed literals; record values and history filters remain parameters. No local file name or search text becomes executable SQL.
- Explorer uses its absolute Windows path and structured arguments. Native DLL imports explicitly search System32. Startup registration uses a quoted absolute application path in the current user's Run key and starts disabled.
- Windows moves use the verified source handle, reject existing destinations, and hold ancestor directory handles against renames. Cross-volume copies preserve alternate streams, verify the main-stream hash before source removal, and preserve ambiguous copies for recovery.
- Organizer actions are serialized. Regression coverage includes overlapping review decisions, changed previews, interrupted journals, concurrent source replacement, and late destination collisions.
- Recycling checks the reviewed hash again in the pre-delete callback. A negative result, missing recycled destination, or unconfirmed shell outcome does not complete the journal entry. However, these checks do not establish recoverability or protect older bin items from capacity eviction. The application has no explicit permanent-delete fallback, but that does not prove the shell preserves the source under every policy.

## Remaining acceptance and limits

Use the [Windows acceptance procedure](WINDOWS_ACCEPTANCE.md) for clean installation, manual organization/undo, tray overflow, sign-in startup, upgrades, uninstall, and disabled/undersized/full Recycle Bin tests. All fixtures must be fictional and all policy changes confined to a disposable guest.

Hyper-V access was initially unavailable on 2026-10-16 and later resolved. The tester subsequently completed clean Windows 11 Pro build 26300 installation and reported passing standard-account package acceptance. Native guest probes then passed ordinary recycle/restore and disabled-bin refusal, but failed the oversized and near-capacity cases. Host and guest checks, their provenance, and remaining manual gates are recorded in [Validation](VALIDATION.md).

Filesystem checks are not a security boundary against another hostile process with the same user's privileges. In particular, Windows shell recycling still requires delete sharing and a path-based handoff; its final callback-to-shell interval is not atomic. Hashes cover the main content stream. Process-interruption tests do not simulate power loss or damaged storage. These limits and the outstanding native/manual checks prevent closing stable-release acceptance merely because scanners pass.
