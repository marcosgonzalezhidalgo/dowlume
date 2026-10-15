# Local validation record

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
