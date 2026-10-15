# Release validation

Target: Windows x64, version 0.1.0. Local validation is recorded in `VALIDATION.md`. A locally built artifact is not a published GitHub release.

## Reproducible packaging

Install a .NET SDK from the **10.0.4xx family** ([SDK policy](../GETTING_STARTED.md#requirements)) and [Inno Setup 6](https://jrsoftware.org/isdl.php), then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1
```

If the compiler is in a custom location, pass `-InnoCompiler "C:\path\ISCC.exe"`. Without a compiler, the script produces the portable ZIP and reports that the installer was not built. Outputs are under `artifacts/`: self-contained publish directory, ZIP, installer when available, and SHA-256 checksums. `-SkipTests` is intended only after the same source revision has already passed validation.

The installer runs without elevation, creates a Start menu shortcut, and retains application data on uninstall. The ZIP includes the runtime. Neither package configures automatic startup. The icon is original code-drawn artwork; regenerate it with `scripts/New-AppIcon.ps1`.

## Before public release

- [x] Verify hosted build, test, and formatting checks for the release-candidate application revision.
- [x] Integrate the five implementation PRs into `main` after their hosted checks pass, preserving their commits.
- [ ] Obtain independent release approval after checking the integrated branch and remaining acceptance results.
- [ ] Install on a clean supported Windows x64 machine without a preinstalled .NET runtime.
- [ ] Confirm first-run preview, cancellation, pause, tray reopen, and exit with a nontechnical tester.
- [ ] Test large downloads and a real move between two physical/local volumes, including an interrupted copy.
- [ ] Verify behavior when Recycle Bin is disabled or full; the source must remain intact.
- [ ] Test installer upgrade and uninstall; user files and local database must remain intact.
- [ ] Decide on release signing and sign the distributable when a certificate is available.
- [ ] Publish release notes, checksums, installer, and ZIP; tag approved source as `v0.1.0`.

Do not mark unavailable hardware tests, clean-machine checks, remote CI, or signing complete based only on unit tests. Cross-volume failure simulations complement but do not replace real-volume testing.
