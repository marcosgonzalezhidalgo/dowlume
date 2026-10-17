# Recycle Bin capacity investigation

Status: **release blocker; preserve the failed guest and do not repeat the failing probes while investigating**. This document records observed evidence and a proposed replacement design. It does not claim that recoverable quarantine has been implemented.

## Candidate and evidence

The tested application source is `cf42326f4560515b1574ebe7794ef645a551bc21`. Installer SHA-256: `73C465CBFC4A47562D4859B9EF9AE0338EF66C9D836C5B07599E4CD37C93F507`. Guest: Windows 11 Pro x64, build 26300, standard `DowlumeTester` account, freshly installed from the verified official ISO. Automated package acceptance passed before the native capacity tests.

The self-contained `FileOperationAcceptance` probe loaded the Core, Infrastructure, and Monitoring assemblies extracted from that exact verified portable ZIP. Each report records the loaded Core/Infrastructure hashes. The guest required no shared .NET runtime or SDK. Worker archive SHA-256: `3583A8649689EAE32E97882FB0ED61C320515387F4647CCBD2168C4A48951F82`.

The tester supplied this native summary, recorded at `2026-10-16T18:46:22.3598918Z`:

| Case | Observed result |
| --- | --- |
| Ordinary 64 KiB recycle and Windows shell restore | Passed; restored content hash matched. |
| Disabled Recycle Bin, 64 KiB sample | Passed; adapter refused and original content remained. |
| 2 MiB sample with 1 MiB bin capacity | Failed: IOException after the source disappeared; subsequent collector found neither oversized source nor any shell-visible bin item in either reported run. See the timing analysis below. |
| Two 768 KiB samples with 1 MiB bin capacity | Failed: the earlier recycled sentinel was no longer present; the incoming sample was recycled and its original source was absent. |
| Original guest policy restoration | Reported successful. |

The harness refuses an initially nonempty bin. It creates only random fictional probe files and checks their hashes. After each case it attempts to restore any remaining probe through the Windows shell, so final source presence must be interpreted alongside the per-operation report. It never empties the bin. A summary failure alone must not be relabeled as proof of a particular native callback sequence.

Three VM checkpoints retain the clean baseline, state after package acceptance/before native probes, and a state after the first failure. The last checkpoint's creation time precedes the second oversized report; do not assume it contains both completed runs. `Collect-WindowsRecycleEvidence.ps1` copies existing JSON reports, records source fixture hashes, and inventories only bin entries originating from the fixture directory. It does not run a probe, restore an item, or change policy. Keep raw guest evidence private; it contains local paths. Do not restore or delete checkpoints, clean fixture folders, empty the bin, or overwrite the original probe reports.

## 2026-10-17: Two-run evidence and restore timing

The tester supplied collector excerpts for two existing runs, not a newly requested rerun. Their oversized reports have timestamps `2026-10-16T18:46:21.5742316Z` and `2026-10-16T18:49:40.815674Z`. Both report `AdapterReturnedSuccess=false`, `System.IO.IOException`, `SourceExists=false`, and `SourceHash=null`. The exception's HRESULT is `-2146232800` (managed IOException); the embedded shell result is the separate value `00270008`. Both loaded Infrastructure SHA-256 `11A0FABFA4DA7EFB6C73D954C56A6E2B662A601C5210819C10699C66C398B197` and Core SHA-256 `AC311864A1B074C3EBA4A0A336181707FB36A3FA7178030F2AC666E5C842B185`.

Each final source inventory contains only the disabled, ordinary, and near-capacity fixtures. Neither oversized nor sentinel fixture remains. The global collector reports `RecycleBinItemCount=0` and `RemainingProbeBinItems=[]`. These are tester-supplied excerpts; the original complete reports remain in the guest. The second run's full summary and the reason it was launched have not been independently established. Preserve both runs without assuming there was only one execution.

Inspection of [the harness](../scripts/Test-WindowsRecycleAcceptance.ps1) and [worker](../scripts/FileOperationAcceptance/RecycleProbe.cs) establishes the following sequence:

1. The worker records source existence immediately after the adapter finishes or throws. That snapshot precedes the harness's restore attempts.
2. `Restore-RecycledProbe` searches the shell bin by expected content hash, refuses an occupied original path, invokes only the shell `undelete` verb, and checks that exact original path and hash. It has no alternate destination or manual copy/move/delete branch.
3. After the oversized source disappears, the harness attempts this restore but discards its Boolean result. It then requires an empty shell bin before proceeding. A later empty bin alone therefore cannot prove whether a restore succeeded; the missing final oversized source shows no successful lasting restoration at the expected location.
4. The near-capacity case measures sentinel retention and incoming-file recycling **before** restoring either item. It restores the incoming item when found, explaining why the immediate report says source absent while the final inventory contains that source. Sentinel disappearance was measured before this restoration; restore cleanup cannot explain that failure.
5. The `finally` block restores registry values and writes the summary/transcript. It does not delete fixture files, empty the bin, or relocate content. The collector copies reports into a new evidence directory and only reads original fixture/bin metadata.

**Conclusion:** the source/bin recoverability requirement failed. The inspected harness provides no cleanup or alternate-location explanation for either missing oversized file. Native permanent deletion is the leading explanation for the oversized outcome, and native capacity eviction is the leading explanation for the missing earlier sentinel. These are inferences from the controlled sequence, not a recorded native deletion trace or proof of physical erasure. The worker did not capture callback flags, destination identity, or the aborted flag; `00270008` alone does not identify which postcondition failed. Further shell operations must remain stopped.

The original collector enumerated the shell namespace, not raw filesystem entries. Its empty result cannot exclude an orphaned bin payload or shell indexing discrepancy. The optional `-IncludeRawBinInventory` collector extension reads direct entries, including hidden files, in the current account's `$Recycle.Bin` directory on the fixture volume and records names, sizes, SHA-256 hashes, and access errors. Compare payload hashes with the existing oversized/sentinel reports. It does not recurse, restore, change permissions, or delete; an incomplete read must never be interpreted as an empty directory. This is a targeted read-only diagnostic, not an unallocated-space forensic scan or an all-volume search. Even an empty raw inventory cannot establish physical irrecoverability.

## Implementation findings

### Raw bin evidence and content recovery

The tester subsequently supplied a complete raw inventory with no access errors: four `$I` files (two 268-byte records and two 258-byte records), `desktop.ini`, and **no `$R` payloads**. This closes the original collector's hidden-file coverage gap for that account and volume at collection time. The subsequent metadata output, collected at `2026-10-17T11:48:09.2455616Z`, supplied the raw bytes. Host-side SHA-256 checks and decoding independently reproduced all four metadata results:

| Metadata record | Run | Original fixture | Original bytes | Metadata deletion time (UTC, 2026-10-16) |
| --- | --- | --- | --- | --- |
| `$I8YR4X7.bin` | First | `probe-ordinary.bin` | 65,536 | 18:46:20.307 |
| `$I73L38B.bin` | First | `probe-near-capacity.bin` | 786,432 | 18:46:22.154 |
| `$IEB7VE0.bin` | Second | `probe-ordinary.bin` | 65,536 | 18:49:39.774 |
| `$I5N7735.bin` | Second | `probe-near-capacity.bin` | 786,432 | 18:49:41.352 |

All four expected content hashes from the associated reports match the corresponding retained-source hashes in the earlier tester-supplied inventory. These are residual metadata records for files the harness restored, **not** records for the missing oversized or sentinel contents. No surviving `$I` or `$R` for either missing case was identified in the complete inventory. There is no remaining ambiguity about which fixtures these four records describe; their absence of paired payloads does not indicate loss of those four restored files.

The [format research by Joachim Metz](https://github.com/libyal/dtformats/blob/main/documentation/Windows%20Recycle.Bin%20file%20formats.asciidoc) describes version-2 records containing original size, deletion FILETIME, and a length-prefixed UTF-16 path. They do not store file content or its hash. The decoded near-capacity and ordinary paths account for the observed 268-byte and 258-byte lengths respectively.

`Read-WindowsRecycleMetadata.ps1` accepts a manifest of the four reported names, sizes, and SHA-256 hashes. It reads through handles that deny writing/deletion, checks the manifest, retains raw bytes in its console output, validates the version/length/encoding/time, and associates only recognized fixture paths with their existing reports. Expected content hashes are explicitly attributed to those reports; they are not recovered from metadata or verified against missing payloads. Unknown formats and read errors remain explicit. The script prints JSON only and does not write a report, restore content, alter permissions, or call the shell adapter. Raw metadata and paths must remain in private acceptance storage.

The current evidence establishes no normal Recycle Bin restore path for the missing contents. It does **not** establish physical irrecoverability. A separate copy of the preserved checkpoint may permit read-only filesystem/deleted-record inspection, with any found content verified against the original report hash. The clean and pre-probe checkpoints predate generation of the random fixtures; recreating a filename or generating new bytes would not recover them. The failure checkpoint was created at `2026-10-16T18:49:38.5401180Z`, before the second oversized report at `18:49:40.815674Z`; its exact captured state needs inspection. Never restore, merge, or delete the original checkpoint to investigate this.

The failure checkpoint was exported to separate private host storage for inspection. Its exported VHDX is self-contained; the original three checkpoints remain present. A read-only Windows mount attempt failed with privilege error `0x80070522` and left the image unattached. A separate host-only DiscUtils 0.16.13 reader then opened the export with `FileAccess.Read` and identified the Windows partition's `-FVE-FS-` BitLocker signature. The image's encrypted Windows partition could not be enumerated as NTFS. **An empty file list from that attempt is not a negative recovery result.** No file contents were restored or recovered, and no recovery utility was installed in the guest. The inspector is kept with the private evidence and introduces no application dependency.

That initial reader limitation did not establish that a recovery password was necessary. The subsequent clear-key inspection below supersedes the assessment that encryption blocked checkpoint access. Keep the earlier reports as historical evidence. Do not boot the exported saved state, since resuming a checkpoint taken around a probe could resume an unwanted test operation.

Microsoft recommends minimizing writes when [attempting deleted-file recovery](https://support.microsoft.com/en-gb/windows/experience/backup-recovery/windows-file-recovery). Do not install recovery tools in the evidence guest or write recovered output onto its system volume. Forensic recovery remains unverified; all application/native implementation changes are on hold while correlating these records and assessing recoverability.

### Checkpoint access with its existing clear key

The user's current-guest report (`FullyEncrypted`, `ProtectionStatus: Off`, no listed key protectors) justified checking the checkpoint metadata before requesting a recovery password. Encryption and protection are separate states: Microsoft's [operations guide](https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/operations-guide) describes pre-provisioned encrypted volumes with a clear protector, and its [FAQ](https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/faq) explains the clear key retained during suspension. The current guest's status alone could not establish the older checkpoint's state.

A new physical copy of the exported VHDX was inspected with read-only file access on the host. Its SHA-256 matched the exported image before inspection and remained unchanged afterward: `3AE5611E04F6031DF15EC3E4F6B78323C3B1D8E0B29650892461866751AC65C5` (25,337,790,464 bytes). The original export, VM, and all checkpoints were left untouched. The copy was neither mounted nor booted; no BitLocker metadata, protectors, protection state, or disk sectors were changed.

The host-only reader used the signed Python Software Foundation 3.13.7 NuGet interpreter with `dissect.fve` 4.6, `dissect.hypervisor` 3.21, `dissect.ntfs` 3.16, and `dissect.volume` 3.18. Package provenance and hashes are retained in the private installation report. These tools are isolated from the application and guest. The reader found **an existing clear key in partition 3**, unlocked it in memory through Dissect's [clear-key reader](https://docs.dissect.tools/en/stable/api/dissect/fve/bde/bde/index.html), verified the decrypted NTFS signature, and read the known fixture directory. No key material was printed or saved. No recovery password, passphrase, or external-key protector was reported by the reader.

The private `CLEAR-KEY-RESULT.json` independently confirms the first run's failed summary, oversized IOException and absent immediate source, sentinel report, and the three surviving source hashes. The ordinary, disabled, and near-capacity fixtures match the previously reported hashes. Oversized and sentinel files are absent from that directory. The second run's directory is absent, consistent with the checkpoint's capture time; this image cannot establish recovery of that later run. These are filesystem observations, not proof that deleted clusters are irrecoverable.

Adding a recovery-password protector to the current guest would write a new protector record wrapping the volume master key into **current** volume metadata. It would not add that record to this older checkpoint. Consequently, the new password alone would not unlock the unmodified old image, even if the underlying volume keys were unchanged: a recovery password is not the volume master key and needs its corresponding stored protector metadata. This follows from the protector structure described in the [BitLocker format research](https://github.com/libyal/libbde/blob/main/documentation/BitLocker%20Drive%20Encryption%20(BDE)%20format.asciidoc). No new protector was created or tested; the existing clear key already provides the required access.

### Native adapter contract

`WindowsRecycleBin` requests `FOFX_RECYCLEONDELETE` with `FOFX_EARLYFAILURE`, `FOF_NOERRORUI`, `FOF_SILENT`, and `FOF_NOCONFIRMATION`. The latter automatically accepts shell confirmations; `FOF_WANTNUKEWARNING`, which requests a warning for permanent destruction, is absent. These documented flag semantics identify a possible silent fallback path, but do not prove the exact callback sequence in these runs. See [Microsoft's operation flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags).

`RecycleOnlyProgressSink.PreDeleteItem` accepts flag `0x80`. Microsoft's [transfer flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-_transfer_source_flags) define that bit as recycling if possible. It is a requested behavior, not an atomic reservation of bin space or proof of subsequent recoverability. Rechecking the source hash prevents acting on changed content, but does not strengthen that native retention contract.

The completion callback checks the incoming item after the shell operation. An exception at that point cannot roll back a native deletion. Neither the callback nor `DownloadOrganizer` reserves capacity, keeps an independent recovery copy, or validates preservation of older bin contents. The near-capacity observation is consistent with shell eviction of the earlier sentinel to admit the new item. It demonstrates that incoming-item success alone does not meet Dowlume's preservation requirement.

The existing unit tests feed chosen flag/result combinations into the sink; they cover callback handling rather than the shell's capacity policy. Ordinary native recycling also cannot establish the disabled/full-bin invariant. The new guest probes are regression cases for that missing coverage, currently failing rather than waived.

Adding a warning flag, checking free disk space, or reading bin capacity before calling the shell would not establish durable recoverability. A size check races with other shell operations; disk free space differs from bin policy; older-item eviction is outside the supplied item's completion check. Storage Sense can also remove recycled content later ([Microsoft documentation](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense)). Native Recycle Bin retention therefore cannot serve as the application's recovery guarantee.

## Proposed recoverable quarantine

Replace the application's native Recycle action with **Move to recovery**, backed by a dedicated `Dowlume Recovery` directory on the same local volume as the managed files. Reserve that folder name against category collisions and exclude its contents from discovery. Explain in the interface that this preserves files and does not free disk space.

1. Validate the reviewed hash and source identity, reject links, and acquire the existing directory leases. Allocate a unique operation directory with a destination that must not exist.
2. Persist the original path, recovery path, content hash, operation ID, and Pending state before changing the filesystem.
3. Rename through the verified source handle on the same volume without replacement. Do not call a shell delete API and do not fall back to cross-volume copy/delete for this action. If metadata space, permissions, volume identity, or destination safety cannot be established, fail with the original source intact.
4. Verify the resulting state and commit the quarantined file plus completed journal entry. Recovery after interruption examines both recorded locations and preserves ambiguous copies; it never guesses which copy to delete.
5. Provide an explicit Restore action that validates content and refuses an occupied original path. Preserve alternate data streams and metadata through the same-volume rename. Keep original operation paths when Settings changes the managed folder.
6. Retain quarantined files indefinitely. No expiry, capacity eviction, background cleanup, automatic permanent deletion, or removal by uninstall. If storage cannot accept an operation, refuse it. Any future permanent-delete feature requires a separate design and explicit user action.

Use a distinct operation kind and file state for quarantine. Keep historical native Recycle records unchanged: they do not acquire a recovery path retroactively, and missing historical files remain unresolved until actual content is recovered. Update the domain model, interface, journal schema compatibility, recovery logic, UI wording, user guide, and acceptance requirements together.

This design protects against ordinary application failures and Windows bin policy; it is not a backup against disk failure or another process deliberately modifying files with the same user's privileges. The source/destination and journal invariants still require validation.

Before implementing the replacement, retain the complete failing evidence and finish the oversized-case diagnosis. Until a safe replacement is implemented and verified, do not use the candidate's Recycle action for valuable files or publish it as stable. The short-term containment option is to disable native recycling and leave files in Review; no containment change or quarantine implementation is claimed in this investigation.

## Required regression coverage for the replacement

- Disabled, undersized, and near-capacity Windows bins do not affect quarantine; the native delete API is never called and existing bin fixtures remain unchanged.
- A full/unwritable recovery location leaves source content intact. Existing destinations, occupied restore paths, changed source content, junctions, and volume changes are refused without overwrites or deletion.
- Move and restore preserve main-stream content, alternate streams, and relevant metadata. Repeated/concurrent decisions cannot quarantine or restore the same operation twice.
- Interrupt before rename, after rename, and before journal completion. Recovery must identify the expected intact copy or retain all ambiguous copies for attention.
- Reopening, changing the managed folder, upgrading, and uninstalling retain both recovery data and its journal. Older native Recycle history remains readable without fabricated recovery destinations.

Do not rerun the known failing capacity probes in the preserved guest to validate a speculative flag change. Once the root cause and replacement are reviewed, use a separate disposable test run and retain both the original evidence and replacement results.

## Isolated acceptance plan (prepared, not executed)

1. Preserve the original VM and all checkpoints. Use a separate disposable guest from a clean baseline for the replacement; never restore the evidence VM to perform a new run. Record the exact candidate, worker, scripts, and fixture hashes before starting.
2. Begin with a non-destructive regression that proves the disabled native action never invokes `IRecycleBin` and leaves reviewed files unchanged. Then validate same-volume move/restore with fictional data and a separate durable fixture manifest outside the operation's working directory.
3. Record source, recovery destination, and existing recovery-item hashes before and immediately after **each** operation. Fail immediately on any unexpected missing or changed content. Record restore attempts and results separately; do not automatically restore or clean up after a failed assertion.
4. Only after safeguards and the test plan are reviewed, exercise quota/space failures, collisions, links, concurrent actions, and interruption points. Keep Windows bin contents unchanged. Simulate failures first; any actual volume-full test must use a dedicated disposable volume, never the host or guest system volume.
5. Independently validate content and alternate streams, crash recovery, app restart, folder-setting changes, upgrade, and uninstall retention. Keep failed runs and new reports separate. Rerun affected package/manual acceptance for changed binaries before considering publication.
