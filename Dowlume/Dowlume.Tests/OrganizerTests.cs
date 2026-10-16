using Dowlume.Core;
using Dowlume.Infrastructure;

namespace Dowlume.Tests;

public sealed class OrganizerTests : IDisposable
{
    private readonly TemporaryWorkspace _workspace = new();
    private readonly SqliteStateStore _store;
    private readonly AppSettings _settings;
    private readonly DownloadOrganizer _organizer;
    private readonly TestClock _clock = new();
    private readonly SafeFileMover _mover = new();

    public OrganizerTests()
    {
        _store = new SqliteStateStore(_workspace.Database);
        _settings = new AppSettings { DownloadsPath = _workspace.Downloads };
        _organizer = new DownloadOrganizer(_store, _mover, new ReadyFiles(), new RefuseRecycle(), _clock);
    }

    [Fact]
    public async Task PreviewDoesNotMoveFilesOrCreateCategories()
    {
        string path = _workspace.CreateFile("report.pdf");
        IReadOnlyList<PreviewItem> preview = await _organizer.PreviewAsync(_settings);
        Assert.Single(preview);
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetDirectories(_workspace.Downloads));
        Assert.Empty(_store.GetFiles());
        Assert.Empty(_store.GetOperations());
    }

    [Fact]
    public async Task OrganizeResolvesCollisionsAndIsIdempotent()
    {
        string existing = _workspace.CreateFile("Documents/report.pdf", "Original");
        string source = _workspace.CreateFile("report.pdf", "New");
        await _organizer.OrganizeAsync(source, _settings);
        await _organizer.OrganizeAsync(source, _settings);
        Assert.Equal("Original", File.ReadAllText(existing));
        TrackedFile file = Assert.Single(_store.GetFiles());
        Assert.EndsWith("report (1).pdf", file.CurrentPath);
        Assert.Equal("New", File.ReadAllText(file.CurrentPath));
        Assert.Equal(OperationStatus.Completed, Assert.Single(_store.GetOperations()).Status);
    }

    [Fact]
    public async Task ReviewAndKeepPersistAcrossRestarts()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("report.pdf"), _settings);
        _clock.Now = _clock.Now.AddDays(31);
        await _organizer.ReconcileAsync(_settings);
        TrackedFile review = Assert.Single(_store.GetFiles());
        Assert.Equal(LifecycleStatus.InReview, review.Status);
        Assert.Contains(AppSettings.ReviewFolderName, review.CurrentPath);
        await _organizer.KeepAsync(review.Id, _settings);
        var restartedStore = new SqliteStateStore(_workspace.Database);
        _clock.Now = _clock.Now.AddYears(10);
        await _organizer.ReconcileAsync(_settings);
        Assert.Equal(LifecycleStatus.Kept, Assert.Single(restartedStore.GetFiles()).Status);
    }

    [Fact]
    public async Task SnoozeDefersReviewForSevenDays()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("setup.exe"), _settings);
        _clock.Now = _clock.Now.AddDays(8);
        await _organizer.ReconcileAsync(_settings);
        await _organizer.SnoozeAsync(Assert.Single(_store.GetFiles()).Id, _settings);
        _clock.Now = _clock.Now.AddDays(6);
        await _organizer.ReconcileAsync(_settings);
        Assert.Equal(LifecycleStatus.Snoozed, Assert.Single(_store.GetFiles()).Status);
        _clock.Now = _clock.Now.AddDays(1);
        await _organizer.ReconcileAsync(_settings);
        Assert.Equal(LifecycleStatus.InReview, Assert.Single(_store.GetFiles()).Status);
    }

    [Fact]
    public async Task UndoPreservesFilesAndPreventsImmediateReorganization()
    {
        string source = _workspace.CreateFile("report.pdf");
        await _organizer.OrganizeAsync(source, _settings);
        await _organizer.UndoAsync(Assert.Single(_store.GetOperations()).Id, _settings);
        await _organizer.OrganizeAsync(source, _settings);
        Assert.True(File.Exists(source));
        Assert.Equal(LifecycleStatus.Kept, Assert.Single(_store.GetFiles()).Status);
    }

    [Fact]
    public async Task UndoRefusesToOverwriteAReplacementFile()
    {
        string source = _workspace.CreateFile("report.pdf");
        await _organizer.OrganizeAsync(source, _settings);
        _workspace.CreateFile("report.pdf", "Replacement");
        await Assert.ThrowsAsync<IOException>(() => _organizer.UndoAsync(Assert.Single(_store.GetOperations()).Id, _settings));
        Assert.Equal("Replacement", File.ReadAllText(source));
        Assert.True(File.Exists(Assert.Single(_store.GetFiles()).CurrentPath));
    }

    [Fact]
    public async Task ChangedContentCannotBeMovedIntoReview()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("report.pdf"), _settings);
        TrackedFile file = Assert.Single(_store.GetFiles());
        File.WriteAllText(file.CurrentPath, "User edits");
        _clock.Now = _clock.Now.AddDays(40);
        await Assert.ThrowsAsync<IOException>(() => _organizer.ReconcileAsync(_settings));
        Assert.Equal("User edits", File.ReadAllText(file.CurrentPath));
        Assert.Equal(OperationStatus.Failed, _store.GetOperations().Last().Status);
    }

    [Fact]
    public async Task RecoversMoveCompletedBeforeDatabaseCommit()
    {
        string source = _workspace.CreateFile("report.pdf");
        string destination = Path.Combine(_workspace.Downloads, "Documents", "report.pdf");
        var file = new TrackedFile(Guid.NewGuid(), source, destination, FileCategory.Documents, _clock.Now, LifecycleStatus.Organized, _mover.Hash(source));
        _store.SaveOperation(new OperationRecord(Guid.NewGuid(), OperationKind.Organize, OperationStatus.Pending, _clock.Now, null, file, source, destination));
        _mover.Move(source, destination, file.ContentHash);
        await _organizer.RetryRecoveryAsync();
        Assert.Equal(file, Assert.Single(_store.GetFiles()));
        Assert.Equal(OperationStatus.Completed, Assert.Single(_store.GetOperations()).Status);
    }

    [Fact]
    public async Task RecoveryLeavesAmbiguousCopiesUntouched()
    {
        string source = _workspace.CreateFile("report.pdf");
        string destination = _workspace.CreateFile("Documents/report.pdf");
        var file = new TrackedFile(Guid.NewGuid(), source, destination, FileCategory.Documents, _clock.Now, LifecycleStatus.Organized, _mover.Hash(source));
        _store.SaveOperation(new OperationRecord(Guid.NewGuid(), OperationKind.Organize, OperationStatus.Pending, _clock.Now, null, file, source, destination));
        await _organizer.RetryRecoveryAsync();
        await _organizer.OrganizeAsync(source, _settings);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(destination));
        Assert.Empty(_store.GetFiles());
        Assert.Equal(OperationStatus.NeedsAttention, Assert.Single(_store.GetOperations()).Status);
    }

    [Fact]
    public async Task CancellationBeforeOperationPreservesSource()
    {
        string source = _workspace.CreateFile("report.pdf");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _organizer.OrganizeAsync(source, _settings, cancellation.Token));
        Assert.True(File.Exists(source));
        Assert.Empty(_store.GetOperations());
    }

    [Fact]
    public async Task ExplicitMoveCanBeUndoneFromAnExternalFolder()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("report.pdf"), _settings);
        _clock.Now = _clock.Now.AddDays(31);
        await _organizer.ReconcileAsync(_settings);
        string external = Directory.CreateDirectory(Path.Combine(_workspace.Root, "Chosen destination")).FullName;
        await _organizer.MoveAsync(Assert.Single(_store.GetFiles()).Id, _settings, external);
        Assert.Equal(LifecycleStatus.Moved, Assert.Single(_store.GetFiles()).Status);
        await _organizer.UndoAsync(_store.GetOperations().Last().Id, _settings);
        Assert.Equal(LifecycleStatus.InReview, Assert.Single(_store.GetFiles()).Status);
    }

    [Fact]
    public async Task RecycleFailureNeverFallsBackToDeletion()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("report.pdf"), _settings);
        _clock.Now = _clock.Now.AddDays(31);
        await _organizer.ReconcileAsync(_settings);
        TrackedFile file = Assert.Single(_store.GetFiles());
        await Assert.ThrowsAsync<IOException>(() => _organizer.RecycleAsync(file.Id, _settings));
        Assert.True(File.Exists(file.CurrentPath));
        Assert.Equal(LifecycleStatus.InReview, Assert.Single(_store.GetFiles()).Status);
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        _store.SaveSettings(_settings);
        AppSettings loaded = _store.LoadSettings()!;
        Assert.Equal(_settings.DownloadsPath, loaded.DownloadsPath);
        Assert.Equal(6, loaded.Categories.Count);
    }

    [Fact]
    public async Task DuplicateConcurrentRequestsProduceOnlyOneMove()
    {
        string source = _workspace.CreateFile("duplicate.pdf");
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => _organizer.OrganizeAsync(source, _settings)));
        Assert.Single(_store.GetFiles());
        Assert.Single(_store.GetOperations());
    }

    [Fact]
    public async Task ConcurrentReviewDecisionsRejectTheStaleSecondAction()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("review.pdf"), _settings);
        _clock.Now = _clock.Now.AddDays(31);
        await _organizer.ReconcileAsync(_settings);
        TrackedFile file = Assert.Single(_store.GetFiles());
        var readiness = new PausedReadiness();
        var organizer = new DownloadOrganizer(_store, _mover, readiness, new RefuseRecycle(), _clock);
        Task keep = organizer.KeepAsync(file.Id, _settings);
        await readiness.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task snooze = organizer.SnoozeAsync(file.Id, _settings);
        Assert.False(snooze.IsCompleted);
        readiness.Resume.SetResult(true);
        await keep;
        await Assert.ThrowsAsync<InvalidOperationException>(() => snooze);
        Assert.Equal(LifecycleStatus.Kept, Assert.Single(_store.GetFiles()).Status);
        Assert.Equal(3, _store.GetOperations().Count);
        Assert.Equal(file.ContentHash, _mover.Hash(Assert.Single(_store.GetFiles()).CurrentPath));
    }

    [Fact]
    public async Task ChangedPreviewIsRejectedBeforeAnyMove()
    {
        string source = _workspace.CreateFile("preview.pdf");
        PreviewItem preview = Assert.Single(await _organizer.PreviewAsync(_settings));
        File.WriteAllText(source, "Changed after preview");
        await Assert.ThrowsAsync<IOException>(() => _organizer.OrganizeAsync(source, _settings, expectedHash: preview.ContentHash));
        Assert.True(File.Exists(source));
        Assert.Empty(_store.GetOperations());
    }

    [Fact]
    public async Task FailureOfOneReviewDoesNotStarveOtherFiles()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("changed.pdf"), _settings);
        await _organizer.OrganizeAsync(_workspace.CreateFile("unchanged.pdf"), _settings);
        File.AppendAllText(_store.GetFiles().First().CurrentPath, "User edits");
        _clock.Now = _clock.Now.AddDays(31);
        await Assert.ThrowsAsync<IOException>(() => _organizer.ReconcileAsync(_settings));
        Assert.Equal(LifecycleStatus.Organized, _store.GetFiles().First().Status);
        Assert.Equal(LifecycleStatus.InReview, _store.GetFiles().Last().Status);
    }

    [Fact]
    public async Task RecoveryBeforeMoveAllowsRetryWithoutLosingFirstAttempt()
    {
        string source = _workspace.CreateFile("report.pdf");
        string destination = Path.Combine(_workspace.Downloads, "Documents", "report.pdf");
        var file = new TrackedFile(Guid.NewGuid(), source, destination, FileCategory.Documents, _clock.Now, LifecycleStatus.Organized, _mover.Hash(source));
        _store.SaveOperation(new OperationRecord(Guid.NewGuid(), OperationKind.Organize, OperationStatus.Pending, _clock.Now, null, file, source, destination));
        await _organizer.RetryRecoveryAsync();
        Assert.Equal(OperationStatus.Failed, Assert.Single(_store.GetOperations()).Status);
        await _organizer.OrganizeAsync(source, _settings);
        Assert.Single(_store.GetFiles());
        Assert.Equal(2, _store.GetOperations().Count);
    }

    [Fact]
    public async Task ModifiedContentCannotBeUndone()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("edited.pdf"), _settings);
        TrackedFile file = Assert.Single(_store.GetFiles());
        File.AppendAllText(file.CurrentPath, "User edits");
        await Assert.ThrowsAsync<IOException>(() => _organizer.UndoAsync(Assert.Single(_store.GetOperations()).Id, _settings));
        Assert.EndsWith("User edits", File.ReadAllText(file.CurrentPath));
        Assert.False(File.Exists(file.OriginalPath));
    }

    [Fact]
    public async Task UndoingReviewDoesNotImmediatelyExpireAgain()
    {
        await _organizer.OrganizeAsync(_workspace.CreateFile("review.pdf"), _settings);
        _clock.Now = _clock.Now.AddDays(31);
        await _organizer.ReconcileAsync(_settings);
        await _organizer.UndoAsync(_store.GetOperations().Last().Id, _settings);
        await _organizer.ReconcileAsync(_settings);
        Assert.Equal(LifecycleStatus.Snoozed, Assert.Single(_store.GetFiles()).Status);
        _clock.Now = _clock.Now.AddDays(7);
        await _organizer.ReconcileAsync(_settings);
        Assert.Equal(LifecycleStatus.InReview, Assert.Single(_store.GetFiles()).Status);
    }

    [Fact]
    public async Task StreamsLargeFilesAndPreservesTheirContent()
    {
        string source = _workspace.CreateFile("large.zip", string.Empty);
        using (var stream = new FileStream(source, FileMode.Open, FileAccess.Write))
        {
            byte[] block = new byte[1024 * 1024];
            for (int index = 0; index < block.Length; index++)
            {
                block[index] = (byte)(index % 251);
            }
            for (int index = 0; index < 32; index++)
            {
                stream.Write(block);
            }
        }
        string expected = _mover.Hash(source);
        await _organizer.OrganizeAsync(source, _settings);
        TrackedFile file = Assert.Single(_store.GetFiles());
        Assert.Equal(expected, _mover.Hash(file.CurrentPath));
        Assert.Equal(32L * 1024 * 1024, new FileInfo(file.CurrentPath).Length);
    }

    public void Dispose() => _workspace.Dispose();

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ReadyFiles : IFileReadiness
    {
        public Task<bool> IsReadyAsync(string path, CancellationToken cancellationToken) => Task.FromResult(File.Exists(path));
    }

    private sealed class RefuseRecycle : IRecycleBin
    {
        public Task RecycleAsync(string path, string expectedHash) => throw new IOException("Simulated unavailable recycle bin.");
    }

    private sealed class PausedReadiness : IFileReadiness
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> IsReadyAsync(string path, CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            return Resume.Task.WaitAsync(cancellationToken);
        }
    }
}
