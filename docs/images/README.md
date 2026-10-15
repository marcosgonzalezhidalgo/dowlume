# Documentation screenshots

These PNGs show the real Avalonia `MainWindow` rendered headlessly on Windows, with staged fictional data. They illustrate separate Overview, Preview, Review, and Activity states; they are not evidence that file operations or native Windows dialogs passed acceptance testing.

| Image | Demonstrates |
| --- | --- |
| `overview.png` | Six managed files, category counts, and two items awaiting review |
| `classification.png` | Six sample downloads mapped to their category folders before organization |
| `review.png` | An archive and installer awaiting an explicit decision |
| `activity.png` | Search, action/status filters, pagination, and selected journal paths |

## Regenerate

From the repository root on Windows with the [required SDK](../../GETTING_STARTED.md#requirements):

```powershell
dotnet run --project scripts/DocumentationScreenshots -c Release
```

Inspect the four PNGs listed above in `artifacts/documentation-screenshots/`, then copy approved captures into `docs/images/`. The renderer also produces `activity-compact.png` at the minimum window size for layout review; that additional capture stays under `artifacts/`. These curated images are versioned; raw test captures and release outputs stay ignored under `artifacts/`.

The renderer uses the application's views, view model, category rules, and an isolated temporary SQLite profile. Download names and the displayed `C:\Dowlume-Demo\Downloads` path are fictional. No download folder is created, watched, or modified; no organize, move, or recycle commands are invoked. The temporary profile is removed when rendering finishes. Re-run and inspect captures after UI changes, and record the date and application revision in [Validation](../VALIDATION.md).
