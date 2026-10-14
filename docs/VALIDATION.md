# Local validation record

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

The native shell test leaves only its own uniquely named sample text file in Windows Recycle Bin. It does not empty the bin. Screenshots and generated packages are ignored under `artifacts/`; test reports are ignored under `TestResults/`.

All five implementation branches are pushed with dependent draft PRs. `main` has not been merged without review. The tagged source and locally packaged binaries correspond to application revision `9322acc`; later documentation-only updates record hosted validation.

The local installer smoke test is not a clean-machine acceptance test. Physical cross-volume interruption, disabled/full Recycle Bin, installer upgrade, release signing, independent review, and public publication remain separate gates in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md). These are not claimed as completed by the local automated suite.
