# Local validation record

## 2026-10-17: Restore timing and two-run evidence review

The tester supplied oversized reports and final fixture inventories for two existing runs, recorded at `2026-10-16T18:46:21.5742316Z` and `2026-10-16T18:49:40.815674Z`. Both oversized reports show IOException with embedded shell result `00270008`, source absent, and matching candidate assembly hashes. Both final inventories lack the oversized and sentinel sources; the shell bin inventory is empty. The reported excerpts were retained separately on the host with their tester-supplied provenance. The second run's full summary has not been independently retrieved.

Source review confirms that the harness never deletes fixtures or chooses an alternative restore directory. It attempts shell restore after the immediate report, explaining the near-capacity source's later presence. Oversized restore outcomes were not recorded separately. No lasting oversized restoration is visible; permanent deletion is the leading explanation, with raw bin entries still unexamined. The optional read-only collector extension can inspect hidden/orphaned payloads without repeating any native operation. See the [timing analysis and isolated acceptance plan](RECYCLE_CAPACITY_INVESTIGATION.md#2026-10-17-two-run-evidence-and-restore-timing).

The host still has the running acceptance guest and all three checkpoints. No failed native test was requested or executed during this investigation. The release remains blocked and unpublished; the recovery-storage proposal remains unimplemented.

`scripts/Test-RecycleEvidenceInventory.ps1` passed five local cases using a new fictional directory: accessible empty inventory, hidden orphan payload with preserved hash/attributes, missing-path error, untraversed directory, and redirected-root refusal. The test loads only the read-only inventory function and never invokes the shell adapter; it is also included in the Windows build workflow. These checks validate the collector, not application recoverability.

## 2026-10-16: Native Recycle Bin capacity failures

The tester supplied the complete native summary recorded at `2026-10-16T18:46:22.3598918Z` from the same Windows 11 Pro build 26300 standard account. The self-contained worker used the exact candidate assemblies extracted from the verified portable archive. Ordinary recycling/Windows shell restore passed with a matching hash, and disabled-bin refusal preserved the source. A 2 MiB sample with a 1 MiB bin failed the source-preservation assertion. Adding a second 768 KiB sample to a 1 MiB bin recycled the incoming item but lost the earlier recycled sentinel. The script reported successful restoration of the original guest policy.

Acceptance is **failed**, not waived. A post-failure checkpoint preserves the guest before read-only evidence collection. The original clean and pre-probe checkpoints, worker, fixtures, and reports are retained. This summary originally described one run; the subsequent collector excerpts identify two existing runs, as recorded above. No rerun was requested by the investigation. See [capacity investigation](RECYCLE_CAPACITY_INVESTIGATION.md) for the implementation findings and proposed same-volume recoverable quarantine. No replacement implementation or release publication is claimed.

## 2026-10-16: Clean-guest package acceptance

The tester supplied the guest launcher's successful JSON summary, recorded at `2026-10-16T18:39:20.3258006Z`, after running the staged kit from the signed-in standard `DowlumeTester` desktop. The guest was freshly installed from the verified official Windows 11 x64 ISO; the tester confirmed no .NET SDK, shared runtime, or development tools were installed. The launcher enforced `-RequireNoSharedDotNet`, verified the standard account and package hashes, and completed successfully. Windows reported Microsoft Windows 11 Pro, version `10.0.26300`, build `26300`.

The tested source is `cf42326f4560515b1574ebe7794ef645a551bc21`; installer SHA-256 is `73C465CBFC4A47562D4859B9EF9AE0338EF66C9D836C5B07599E4CD37C93F507`. The tester explicitly confirmed native notification-area registration and restoration from hidden/minimized states. The completed script also covers portable launch/exit, previous-candidate upgrade, duplicate-instance and running-app guards, startup-entry retention/removal, uninstall, and profile/fictional-file preservation. The full `package-result.json` and transcript remain in the guest; the tester-supplied summary is retained separately on the host, with its provenance identified. This is a reported guest result, not an independently retrieved raw report.

The host retained clean and pre-Recycle-Bin checkpoints. Manual first use, actual sign-in/startup behavior, native disabled/full-bin checks, Windows restore, and independent review remain separate gates. The release has not been published.

## 2026-10-16: Hyper-V guest preparation

Hyper-V access now works from the development session; `Get-VM` initially returned no guests. A dedicated Generation 2 VM was created with two virtual CPUs, 4–6 GiB dynamic memory, a 96 GiB dynamic disk, Secure Boot, and virtual TPM. The firmware/security settings were queried after creation. The VM was initially left powered off pending official Windows 11 x64 media and interactive Windows setup. No guest acceptance test has run.

The exact clean `cf42326f4560515b1574ebe7794ef645a551bc21` candidate and the earlier `cdc5e35` installer were staged in dedicated test storage. All four release checksums and the earlier installer's SHA-256 matched. The preparation script and [Hyper-V handoff](HYPER_V_ACCEPTANCE.md) preserve the distinction between provisioning, automated interactive smoke, and manual acceptance. Microsoft's automated ISO link request was blocked; a browser download was requested. Personal files and host Recycle Bin policy were not changed, and the release remains unpublished.

The user subsequently supplied the official English International x64 ISO. Its SHA-256 matched Microsoft's published value; [media details](HYPER_V_ACCEPTANCE.md#prepared-host-state--2026-10-16) are recorded with the VM handoff. The ISO is attached, the VM is running, and its console is open for interactive Windows setup. Preparation [PR #14](https://github.com/mglezz/dowlume/pull/14) passed build/test and security workflows before merging. Guest setup and acceptance remain pending.

## 2026-10-16: Background access, race review, and security scans

Tray/startup implementation: `3218f6a` (PR #12, merged after both hosted workflows passed). File-operation hardening: `eb01156`. All file-operation fixtures were generated for testing. No personal Downloads, host Recycle Bin policy, or VM was modified.

| Check | Result |
| --- | --- |
| Regression suite | 72 tests passed: 70 regular tests and two native WindowsShell tests; no failures or skips. Closing during a pending confirmation cancels it without organizing files or hanging shutdown. |
| Tray regression | Application-level attachment, reopening a hidden/minimized window from both commands, and disposal covered headlessly. Native Windows icon registration and UI-Automation Hide to tray followed by native click delivery also passed. |
| Windows startup | Default-off behavior, failure feedback, quoted paths, ownership-scoped removal, and unsupported hosts covered. Packaged upgrade retained the fixture startup entry; uninstall removed it. Actual sign-in remains a VM/manual check. |
| Race conditions | Verified source handles refused writes/renames/replacement; late destination collisions preserved both files; directory leases blocked ancestor renames. Overlapping review actions rejected the stale second decision. |
| Cross-volume files | A 64 MiB sample and an alternate data stream survived move and undo between the NVMe system volume and a separate SATA disk. Interrupting the 512 MiB worker preserved source/partial copy, marked NeedsAttention, and blocked another move. |
| Recycling | Ordinary native recycling and changed-content refusal passed. Callback tests cover permanent-delete refusal, changed content, missing recycled destination, disk-full HRESULT, and unconfirmed outcomes. These simulations do not certify disabled/full Windows bin policy. |
| Dependencies | No vulnerable direct/transitive packages reported by the configured NuGet sources. |
| Code scan | All .NET security analyzers passed for the solution and both helper projects with warnings treated as errors. SQL statements use a fixed private table selection and parameterized data; native library and Explorer lookup use system locations. |
| Secrets | Gitleaks 8.30.1 passed for all local Git refs and tracked/unignored source snapshots, with redacted reports. The scanner archive matched its pinned SHA-256. |
| Packaging | Local portable launch, previous-draft upgrade, running-app refusal, tray restoration, startup registration, graceful exit, uninstall, and fixture/profile preservation passed. |
| Hyper-V | The host service is running. Repeated host PowerShell `Get-VM` checks returned permission denied; no clean guest or manual VM workflow was validated. |

Reports are retained under `artifacts/security-20261016-final/` and `artifacts/acceptance/`, including `cross-volume-handle-streams.json` and `package-security-20261016.json`. The preliminary package was built with a dirty tree and is not an approved release artifact; rebuild the merged revision and repeat package checks before replacing draft assets. The dedicated security workflow repeats the scans and retains redacted reports.

Security/stability acceptance remains open for clean-guest installation, actual sign-in, manual organization/undo/upgrade/uninstall, disabled/full Recycle Bin policy, and independent first-use/release review. See [security review](SECURITY_REVIEW.md) for scope and residual race limits. Passing scanners does not authorize marking unavailable manual tests complete.

## 2026-10-16: Windows release hardening

Implementation revision: `d01ae7b`. Environment: Windows 11 Pro x64 build 26200, .NET SDK 10.0.401, Inno Setup 6.7.3. File-operation tests used unique disposable directories; personal Downloads and Recycle Bin settings were not modified. The user confirmed that no clean Windows environment is currently available.

| Check | Result |
| --- | --- |
| Automated regression suite | 54 non-shell tests passed in Release configuration. |
| Native recycling | Separate WindowsShell test passed with the verified read handle; one uniquely named disposable file was recycled. The bin was not emptied. |
| Physical cross-volume move | A 64 MiB sample moved from the NVMe system volume to a separate SATA disk and was restored by Undo; hashes matched. |
| Interrupted physical copy | The isolated worker was terminated during a 512 MiB cross-volume copy. The original hash remained intact, the partial destination differed, recovery preserved both, marked NeedsAttention, and refused another move. |
| Portable and installer | ZIP extraction/launch/exit, per-user installation, upgrade from the earlier local candidate, and uninstall passed. The real SQLite profile and a sample user file were retained unchanged by install/uninstall. |
| Running application protection | Upgrade and uninstall both refused while Dowlume was running; executable content remained unchanged. Duplicate-profile startup was refused and normal shutdown completed. |
| Packaging checks | Existing output directory refused; runtime-free acceptance mode correctly refused the development PC. Offline guide and bundled runtime/native libraries present. |
| Formatting and dependencies | Solution and acceptance-harness formatting passed; PowerShell scripts parsed; no vulnerable packages reported by configured NuGet sources. |

The preliminary installer smoke used SHA-256 `379132929097280BF574461AFDD25FD70C79F32431169AEAA29BE85CCB6DC130`; the earlier candidate was `AE7AB83D591A82DE574B253141D705D4923E747F5F84E3772185649417D2CA36`. These preliminary packages were built before committing and are marked dirty in their build information. They establish the local checks above, not approval of final release assets. Final packages must be rebuilt from the clean merged revision and have their own smoke report and hashes.

Raw reports remain under `artifacts/acceptance/` because they contain local paths. Cross-volume fixtures were retained for inspection. These tests cover process interruption, not power loss or physical disk failure. Clean-machine installation without .NET, disabled/full Recycle Bin behavior, independent first-use testing, and final release review remain open. The [acceptance procedure](WINDOWS_ACCEPTANCE.md) and [release checklist](RELEASE_CHECKLIST.md) define the handoff; stable `v0.1.0` publication is not yet justified.

## 2026-10-16: Searchable activity history

Environment: Windows x64, .NET SDK 10.0.401. Application revision: `ce28fda`. This change extends Activity beyond the former 200-entry display limit; the tagged release candidate and existing local packages are unchanged.

| Check | Result |
| --- | --- |
| Release build and automated suite | `dotnet test Dowlume/Dowlume.slnx -c Release --no-restore --filter "Category!=WindowsShell"` passed all 54 tests, with no failures or skips. |
| History storage | All 251 fixture entries are reachable without duplication; paging survives reopening the database and new arrivals between reads. Updated statuses keep their journal position. |
| Search and counts | Unicode case comparison, literal SQL/wildcard characters, source/destination paths, null destinations, combined filters, empty results, page clamping, invalid inputs, and independent attention counts covered. |
| Desktop workflow | Browse older pages, retain selection during refresh/new arrivals, search an action older than 200 entries, undo it through the existing safety checks, clear filters, and render the new controls. |
| Activity screenshots | Fictional-data renderer completed successfully. Activity inspected at 1120 × 820 and 880 × 680; normal-size capture committed in [images](images/README.md). |
| Formatting and whitespace | `dotnet format --verify-no-changes --no-restore` passed for the solution and screenshot renderer; `git diff --check` passed. |
| Dependency audit | `dotnet list Dowlume/Dowlume.slnx package --vulnerable --include-transitive` reported no vulnerable packages from configured sources. |
| Documentation | Local Markdown file/image targets checked; user guide, architecture, roadmap, and [platform assessment](PLATFORM_SUPPORT.md) updated. |

Pagination uses schema version 1 without migrating or deleting history. Counts and search still scan journal JSON; organizer/recovery queries still load complete collections. This is a bounded history display, not a claim of million-file scalability.

Native recycle and installer code were not changed or revalidated in this increment. Clean-machine, physical cross-volume, and remaining release acceptance gates stay open. macOS and Linux ports are deferred until their native adapters and platform safety checks are implemented and validated.

## 2026-10-15: Domain-model documentation

Scope: the [domain model](requirements/DOMAIN_MODEL.md), its UML sources and SVGs, documentation links, and a standalone rendering script. Application source is unchanged from `a4b1c9a`. The model was checked against the current implementation and existing tests; no new application acceptance results are claimed.

| Check | Result |
| --- | --- |
| PlantUML syntax and generation | All ten sources passed syntax validation and SVG/PNG generation with PlantUML 1.2026.8 and Eclipse Temurin Java 21.0.12.1 on Windows x64. |
| SVG structure | Every generated file parsed as SVG XML; no embedded scripts or HTML were present. |
| Visual rendering | All ten final SVGs opened in headless Chrome with an isolated profile and were visually inspected. PlantUML PNG previews and direct SVG rasterization also completed. Labels, multiplicities, branches, and object-instance notation were checked. |
| Semantic consistency | Reviewed candidate selection, association multiplicities, transient versus tracked facts, the dated object scenario, both lifecycles, decision/undo outcomes, and recovery alternatives against the MVP code. This is an analysis review, not independent domain-expert approval. |
| Links and scope | Local Markdown links and anchors passed; all ten images and PlantUML sources are linked from the model. No actor, requirements, or use-case specification was added. |
| Tooling and whitespace | The rendering script executed successfully and passed PowerShell parsing; `git diff --check` passed. |

The [diagram README](requirements/diagrams/domain-model/README.md) records the pinned renderer, checksum, and reproduction command. Tool binaries and visual-review captures remain ignored under `artifacts/`. Runtime, shell, installer, and physical cross-volume tests were not repeated for this documentation-only change; the earlier evidence and remaining release gates are preserved below.

## 2026-10-15: Documentation and screenshots

Environment: Windows x64, .NET SDK 10.0.401. Application source is unchanged from `40b7537`; this update adds documentation and a standalone screenshot renderer.

| Check | Result |
| --- | --- |
| Release build and automated suite | `dotnet test Dowlume/Dowlume.slnx -c Release --no-restore --filter "Category!=WindowsShell"` built the solution and passed all 44 tests, with no failures or skips. |
| Documentation screenshots | `dotnet run --project scripts/DocumentationScreenshots -c Release` compiled and exited successfully. All three 1120 × 820 PNGs were visually inspected: Overview, classification Preview, and Review. |
| Screenshot privacy and provenance | Real Avalonia views rendered with fictional filenames and a demonstration path. The isolated SQLite profile is temporary; no download files are accessed. Curated captures and regeneration instructions are in [images](images/README.md). |
| Formatting and whitespace | `dotnet format --verify-no-changes --no-restore` passed for the application solution and the standalone renderer; `git diff --check` passed. |
| Documentation links | Relative file/image links and heading anchors checked against the repository. |
| Renderer dependency audit | `dotnet list scripts/DocumentationScreenshots/DocumentationScreenshots.csproj package --vulnerable --include-transitive` reported no vulnerable packages from the configured sources. |

Staged screenshots illustrate the interface, not completed filesystem operations. Native shell, installer, clean-machine, and physical cross-volume acceptance checks were not repeated for this documentation update. Earlier evidence and outstanding release gates remain below.

## 2026-10-14: Release-candidate application

Validation date: 2026-10-14. Environment: Windows 11 x64 (build 26200), .NET SDK 10.0.401. All file-operation fixtures used disposable temporary directories; personal Downloads was not used.

| Check | Result |
| --- | --- |
| Release solution build | Passed, zero warnings and errors. |
| Automated suite excluding native shell | 44 passed, zero failures or skips. |
| Native Windows Recycle Bin test | Passed separately, including the verified read handle used by the organizer. |
| Headless desktop workflow | Preview is non-mutating; declined consent preserves source; accepted consent starts monitoring; pause, review, and Keep work. |
| Headless rendering | All five tabs rendered; Overview and Settings captures inspected. |
| Filesystem safety | Collisions, changed content, stale previews, cancellation, locked files, invalid destinations, directory boundary checks, junction rejection, concurrent duplicates, and a 32 MiB file covered. |
| Recovery | Interruptions before move and after move/before state commit, ambiguous copies, and continued processing after one review failure covered. |
| Monitoring | Bounded queue saturation, 40-file bursts, incomplete extensions, rename, and restart discovery covered. |
| Dependency vulnerability audit | No vulnerable packages reported by configured NuGet sources, including transitive packages. |
| Formatting and whitespace | `dotnet format --verify-no-changes` and `git diff --check` passed. |
| Hosted CI | [Build, test, and formatting passed](https://github.com/marcosgonzalezhidalgo/dowlume/actions/runs/38627471880) for application revision `9322acc`, tagged `v0.1.0-rc.1`. |
| Windows package generation | Self-contained x64 publish, portable ZIP, Inno Setup installer, and SHA-256 checksums generated locally. |
| Packaged executable | Final binary started with an isolated profile, refused a second instance using that profile, and exited gracefully. |
| Installer/uninstaller smoke | Per-user installation into an isolated workspace directory and uninstall passed; the isolated profile was retained. |

The native shell test leaves only its own uniquely named sample text file in Windows Recycle Bin. It does not empty the bin. Raw test screenshots and generated packages are ignored under `artifacts/`; test reports are ignored under `TestResults/`. Curated documentation images are versioned separately under `docs/images/`.

On 2026-10-15, implementation PRs #1 through #5 were merged into `main` after their hosted checks passed. The integrated tree at `43992f3` was compared with the tested release branch at `16facb0` and is identical. The tagged source and locally packaged binaries still correspond to application revision `9322acc`; later documentation-only updates record validation and integration history.

The local installer smoke test is not a clean-machine acceptance test. Physical cross-volume interruption, disabled/full Recycle Bin, installer upgrade, release signing, independent review, and public publication remain separate gates in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md). These are not claimed as completed by the local automated suite.
