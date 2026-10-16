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
| 2 MiB sample with 1 MiB bin capacity | Failed the source-preservation assertion. Detailed adapter report and post-test fixture state are required to distinguish permanent deletion from recycling followed by the harness's restore attempt. |
| Two 768 KiB samples with 1 MiB bin capacity | Failed: the earlier recycled sentinel was no longer present; the incoming sample was recycled and its original source was absent. |
| Original guest policy restoration | Reported successful. |

The harness refuses an initially nonempty bin. It creates only random fictional probe files and checks their hashes. After each case it attempts to restore any remaining probe through the Windows shell, so final source presence must be interpreted alongside the per-operation report. It never empties the bin. A summary failure alone must not be relabeled as proof of a particular native callback sequence.

Three VM checkpoints retain the clean baseline, state after package acceptance/before native probes, and failed state before evidence collection. `Collect-WindowsRecycleEvidence.ps1` copies existing JSON reports, records source fixture hashes, and inventories only bin entries originating from the fixture directory. It does not run a probe, restore an item, or change policy. Keep raw guest evidence private; it contains local paths. Do not restore or delete checkpoints, clean fixture folders, empty the bin, or overwrite the original probe reports.

## Implementation findings

`WindowsRecycleBin` requests `FOFX_RECYCLEONDELETE` with `FOFX_EARLYFAILURE`, `FOF_NOERRORUI`, `FOF_SILENT`, and `FOF_NOCONFIRMATION`. The latter automatically accepts shell confirmations; `FOF_WANTNUKEWARNING`, which requests a warning for permanent destruction, is absent. These documented flag semantics identify a possible silent fallback path, but the oversized fixture's detailed evidence is needed before attributing its exact outcome. See [Microsoft's operation flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags).

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
