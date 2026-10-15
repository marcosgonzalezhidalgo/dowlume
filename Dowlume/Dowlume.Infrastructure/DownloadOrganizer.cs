using Dowlume.Core;

namespace Dowlume.Infrastructure;

public sealed class DownloadOrganizer(
    IStateStore store,
    SafeFileMover mover,
    IFileReadiness readiness,
    IRecycleBin recycleBin,
    TimeProvider? timeProvider = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task<IReadOnlyList<PreviewItem>> PreviewAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        PathSafety.ValidateRoot(settings.DownloadsPath);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var result = new List<PreviewItem>();
            IReadOnlyList<TrackedFile> tracked = store.GetFiles();
            foreach (string path in Directory.EnumerateFiles(settings.DownloadsPath).Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CanDiscover(path, settings, tracked) || !await readiness.IsReadyAsync(path, cancellationToken)) { continue; }
                CategoryRule rule = FileClassifier.Classify(path, settings);
                string destination = PathSafety.AvailableDestination(Path.Combine(settings.DownloadsPath, rule.FolderName), Path.GetFileName(path));
                result.Add(new PreviewItem(path, destination, rule.Category, mover.Hash(path)));
            }
            return result;
        }
        finally { _gate.Release(); }
    }

    public async Task OrganizeAsync(string path, AppSettings settings, CancellationToken cancellationToken = default, string? expectedHash = null)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!CanDiscover(path, settings, store.GetFiles()) || !await readiness.IsReadyAsync(path, cancellationToken)) { return; }
            string hash = mover.Hash(path);
            if (expectedHash is not null && hash != expectedHash)
            {
                throw new IOException("A previewed file changed. Refresh the preview before organizing it.");
            }
            CategoryRule rule = FileClassifier.Classify(path, settings);
            string destination = PathSafety.AvailableDestination(Path.Combine(settings.DownloadsPath, rule.FolderName), Path.GetFileName(path));
            var file = new TrackedFile(Guid.NewGuid(), path, destination, rule.Category, _clock.GetUtcNow(), LifecycleStatus.Organized, hash);
            await ExecuteAsync(OperationKind.Organize, null, file, path, destination, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task ReconcileAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RecoverPendingOperations();
            foreach (TrackedFile file in store.GetFiles().Where(file => file.IsManaged && PathSafety.IsWithin(settings.DownloadsPath, file.CurrentPath)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsBlocked(file.Id, file.CurrentPath)) { continue; }
                PathSafety.RequireWithin(settings.DownloadsPath, file.CurrentPath);
                if (!File.Exists(file.CurrentPath))
                {
                    store.SaveFile(file with { Status = LifecycleStatus.Missing });
                    continue;
                }
                if (!LifecyclePolicy.IsDue(file, settings, _clock.GetUtcNow())) { continue; }
                if (!await readiness.IsReadyAsync(file.CurrentPath, cancellationToken)) { continue; }
                string destination = PathSafety.AvailableDestination(Path.Combine(settings.DownloadsPath, AppSettings.ReviewFolderName), file.Name);
                await ExecuteAsync(OperationKind.Review, file, file with { CurrentPath = destination, Status = LifecycleStatus.InReview }, file.CurrentPath, destination, cancellationToken);
            }
        }
        finally { _gate.Release(); }
    }

    public Task KeepAsync(Guid fileId, AppSettings settings, CancellationToken cancellationToken = default) =>
        DecideAsync(fileId, settings, OperationKind.Keep, null, cancellationToken);

    public Task SnoozeAsync(Guid fileId, AppSettings settings, CancellationToken cancellationToken = default) =>
        DecideAsync(fileId, settings, OperationKind.Snooze, null, cancellationToken);

    public Task MoveAsync(Guid fileId, AppSettings settings, string destinationFolder, CancellationToken cancellationToken = default) =>
        DecideAsync(fileId, settings, OperationKind.Move, destinationFolder, cancellationToken);

    public Task RecycleAsync(Guid fileId, AppSettings settings, CancellationToken cancellationToken = default) =>
        DecideAsync(fileId, settings, OperationKind.Recycle, null, cancellationToken);

    private async Task DecideAsync(Guid fileId, AppSettings settings, OperationKind kind, string? destinationFolder, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            TrackedFile before = GetActionableFile(fileId, settings);
            if (before.Status != LifecycleStatus.InReview)
            {
                throw new InvalidOperationException("This file is no longer awaiting review. Refresh the list.");
            }
            if (!await readiness.IsReadyAsync(before.CurrentPath, cancellationToken))
            {
                throw new IOException("The file is busy or still changing. Try again after it finishes.");
            }
            LifecycleStatus status = kind switch
            {
                OperationKind.Keep => LifecycleStatus.Kept,
                OperationKind.Snooze => LifecycleStatus.Snoozed,
                OperationKind.Move => LifecycleStatus.Moved,
                OperationKind.Recycle => LifecycleStatus.Recycled,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            string? destination = null;
            if (kind != OperationKind.Recycle)
            {
                string folder = destinationFolder ?? Path.Combine(settings.DownloadsPath, settings.Categories.Single(rule => rule.Category == before.Category).FolderName);
                if (destinationFolder is not null)
                {
                    PathSafety.ValidateRoot(folder);
                    if (string.Equals(Path.GetFullPath(folder), Path.GetDirectoryName(before.CurrentPath), PathSafety.Comparison) ||
                        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)), Path.TrimEndingDirectorySeparator(settings.DownloadsPath), PathSafety.Comparison))
                    {
                        throw new ArgumentException("Choose a destination outside the review folder and Downloads root.");
                    }
                }
                destination = PathSafety.AvailableDestination(folder, before.Name);
            }
            TrackedFile after = before with
            {
                CurrentPath = destination ?? before.CurrentPath,
                Status = status,
                SnoozedUntilUtc = kind == OperationKind.Snooze ? _clock.GetUtcNow().AddDays(7) : null
            };
            await ExecuteAsync(kind, before, after, before.CurrentPath, destination, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task UndoAsync(Guid operationId, AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IReadOnlyList<OperationRecord> history = store.GetOperations();
            OperationRecord operation = history.Single(record => record.Id == operationId);
            if (!operation.CanUndo || history.Any(record => record.UndoesOperationId == operationId && record.Status == OperationStatus.Completed))
            {
                throw new InvalidOperationException("This action cannot be undone. Recycled files can be restored through Windows Recycle Bin.");
            }
            TrackedFile current = store.GetFiles().Single(file => file.Id == operation.After.Id);
            if (current != operation.After || history.Last(record => record.After.Id == current.Id && record.Status == OperationStatus.Completed).Id != operation.Id || IsBlocked(current.Id, current.CurrentPath))
            {
                throw new InvalidOperationException("A later action changed this file. Undo its latest eligible action first.");
            }
            // Undo may read an explicitly chosen external move destination, but must return inside Downloads.
            PathSafety.RequireWithin(settings.DownloadsPath, operation.SourcePath);
            PathSafety.RejectLinks(current.CurrentPath);
            if (!await readiness.IsReadyAsync(current.CurrentPath, cancellationToken)) { throw new IOException("The file is busy or missing."); }
            TrackedFile restored = operation.Before ?? current with { CurrentPath = operation.SourcePath, Status = LifecycleStatus.Kept };
            await ExecuteAsync(OperationKind.Undo, current, restored, current.CurrentPath, operation.SourcePath, cancellationToken, operation.Id);
        }
        finally { _gate.Release(); }
    }

    public async Task RetryRecoveryAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { RecoverPendingOperations(); }
        finally { _gate.Release(); }
    }

    private TrackedFile GetActionableFile(Guid fileId, AppSettings settings)
    {
        TrackedFile file = store.GetFiles().Single(item => item.Id == fileId);
        PathSafety.RequireWithin(settings.DownloadsPath, file.CurrentPath);
        if (IsBlocked(file.Id, file.CurrentPath)) { throw new IOException("Resolve this file's interrupted operation in Activity before continuing."); }
        return file;
    }

    private bool CanDiscover(string path, AppSettings settings, IReadOnlyList<TrackedFile> tracked)
    {
        PathSafety.RequireWithin(settings.DownloadsPath, path);
        return File.Exists(path) &&
               string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.DownloadsPath)), PathSafety.Comparison) &&
               !FileClassifier.IsExcluded(path, settings) &&
               !tracked.Any(file => file.IsManaged && string.Equals(file.CurrentPath, path, PathSafety.Comparison)) &&
               !IsBlocked(null, path);
    }

    private bool IsBlocked(Guid? fileId, string path) => store.GetOperations().Any(operation =>
        operation.Status is OperationStatus.Pending or OperationStatus.NeedsAttention &&
        (operation.After.Id == fileId || string.Equals(operation.SourcePath, path, PathSafety.Comparison) || string.Equals(operation.DestinationPath, path, PathSafety.Comparison)));

    private async Task ExecuteAsync(OperationKind kind, TrackedFile? before, TrackedFile after, string source, string? destination, CancellationToken cancellationToken, Guid? undoes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operation = new OperationRecord(Guid.NewGuid(), kind, OperationStatus.Pending, _clock.GetUtcNow(), before, after, source, destination, UndoesOperationId: undoes);
        store.SaveOperation(operation);
        try
        {
            // Once journaled, finish this operation even if shutdown was requested.
            if (kind == OperationKind.Recycle)
            {
                if (mover.Hash(source) != after.ContentHash) { throw new IOException("The file changed since it was tracked."); }
                await recycleBin.RecycleAsync(source);
            }
            else
            {
                mover.Move(source, destination!, after.ContentHash);
            }
            store.CompleteOperation(operation);
        }
        catch (Exception error)
        {
            // A failed persistence write after a move must remain recoverable.
            bool sourceRemains = File.Exists(source);
            bool destinationExists = destination is not null && File.Exists(destination);
            OperationStatus status = sourceRemains && !destinationExists ? OperationStatus.Failed : OperationStatus.NeedsAttention;
            store.SaveOperation(operation with { Status = status, Error = FriendlyError(error) });
            throw;
        }
    }

    private void RecoverPendingOperations()
    {
        foreach (OperationRecord operation in store.GetOperations().Where(record => record.Status is OperationStatus.Pending or OperationStatus.NeedsAttention))
        {
            try
            {
                PathSafety.RejectLinks(operation.SourcePath);
                bool sourceExists = File.Exists(operation.SourcePath);
                if (operation.DestinationPath is { } destination)
                {
                    PathSafety.RejectLinks(destination);
                    bool destinationExists = File.Exists(destination);
                    if (!sourceExists && destinationExists && mover.Hash(destination) == operation.After.ContentHash)
                    {
                        store.CompleteOperation(operation);
                        continue;
                    }
                    if (sourceExists && !destinationExists && mover.Hash(operation.SourcePath) == operation.After.ContentHash)
                    {
                        store.SaveOperation(operation with { Status = OperationStatus.Failed, Error = "Interrupted before moving. The source is intact; retry the original action." });
                        continue;
                    }
                }
                else if (sourceExists && mover.Hash(operation.SourcePath) == operation.After.ContentHash)
                {
                    store.SaveOperation(operation with { Status = OperationStatus.Failed, Error = "Recycling did not finish. The source is intact; retry the action." });
                    continue;
                }
                store.SaveOperation(operation with { Status = OperationStatus.NeedsAttention, Error = "Inspect the source and destination in Explorer, then retry recovery. Nothing will be removed automatically." });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                store.SaveOperation(operation with { Status = OperationStatus.NeedsAttention, Error = FriendlyError(error) });
            }
        }
    }

    public static string FriendlyError(Exception error) => error switch
    {
        UnauthorizedAccessException => "Access denied. Check folder permissions and retry.",
        IOException => "The file is busy, changed, missing, or its destination is unavailable. Inspect it in Explorer and retry.",
        OperationCanceledException => "The operation was canceled; unfinished work can be retried.",
        ArgumentException or InvalidOperationException => error.Message,
        _ => "The operation failed. Local diagnostics contain a technical error code. Restart and retry recovery."
    };
}
