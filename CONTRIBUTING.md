# Contributing

Use English for code, comments, documentation, UI, branches, and commit messages.

Prefer names that express the domain, small cohesive methods, immutable records, constructor injection, and explicit failure handling. Keep business decisions out of views. Avoid speculative abstractions, service locators, duplicate rules, and comments that merely restate the code. Comments should explain constraints or non-obvious safety decisions. These practices follow the readability and single-responsibility principles discussed in *Clean Code*; safety and clarity take priority over making methods artificially short.

## Validation

```powershell
dotnet restore Dowlume/Dowlume.slnx
dotnet build Dowlume/Dowlume.slnx -c Release --no-restore
dotnet test Dowlume/Dowlume.slnx -c Release --no-build --filter "Category!=WindowsShell"
dotnet format Dowlume/Dowlume.slnx --verify-no-changes --no-restore
dotnet list Dowlume/Dowlume.slnx package --vulnerable --include-transitive
```

Run the Windows shell test separately on a Windows desktop with a working Recycle Bin:

```powershell
dotnet test Dowlume/Dowlume.slnx -c Release --filter "Category=WindowsShell"
```

That test recycles one uniquely named disposable text file and leaves it in the bin. It never empties the bin. All filesystem tests use randomly named temporary directories; never point tests at personal Downloads. Headless tests use the real views, SQLite, and filesystem with fake confirmation dialogs.

Set `DOWLUME_TEST_SCREENSHOTS` to an output directory when running the desktop test to capture all five screens. Do not commit captures containing personal paths.

## Git workflow

`main` is the only permanent branch. Normally create a short-lived topic branch from current `main`, make focused Conventional Commits, and submit a pull request. Merge after review and passing CI. Do not force-push shared history.

The initial implementation was merged through five dependent PRs. Their original commits and merge history are preserved; see [Git history and branch policy](docs/BRANCHES.md). New work starts from the current `main`:

```powershell
git switch main
git pull --ff-only origin main
git switch -c feature/describe-the-change
```

After validation, commit and push the topic branch, open a PR into `main`, and merge after review and passing checks. Delete the merged branch, then synchronize local `main` with another fast-forward pull. Use merge commits when preserving a dependent branch stack; ordinary topic PRs may be squashed if their final commit clearly describes the complete change.

Meaningful safety tests are required for changed file-operation behavior. Do not add tests that only mirror property getters or implementation details. Keep all release checkboxes tied to observed evidence.
