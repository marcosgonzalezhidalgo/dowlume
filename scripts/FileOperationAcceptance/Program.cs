using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Dowlume.Core;
using Dowlume.Infrastructure;
using Dowlume.Monitoring;

namespace FileOperationAcceptance;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This acceptance harness requires Windows.");
        }
        if (args is ["move", var database, var fileId, var destination])
        {
            var store = new SqliteStateStore(database);
            await CreateOrganizer(store).MoveAsync(Guid.Parse(fileId), store.LoadSettings()!, destination);
            return;
        }
        if (args is not ["verify", var secondVolumeFolder, var reportPath])
        {
            throw new ArgumentException("Usage: FileOperationAcceptance verify <folder-on-second-volume> <report.json>");
        }
        await VerifyAsync(Path.GetFullPath(secondVolumeFolder), Path.GetFullPath(reportPath));
    }

    private static async Task VerifyAsync(string secondVolumeFolder, string reportPath)
    {
        string runId = Guid.NewGuid().ToString("N");
        string sourceRoot = Path.Combine(Path.GetTempPath(), "Dowlume.Acceptance", runId);
        string destinationRoot = Path.Combine(secondVolumeFolder, runId);
        Require(!string.Equals(Path.GetPathRoot(sourceRoot), Path.GetPathRoot(destinationRoot), StringComparison.OrdinalIgnoreCase),
            "Choose a destination on a different local volume.");
        Require(new DriveInfo(Path.GetPathRoot(destinationRoot)!).DriveType == DriveType.Fixed, "The second volume must be a fixed local disk.");
        PathSafety.RejectLinks(sourceRoot);
        PathSafety.RejectLinks(destinationRoot);
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(destinationRoot);
        Console.WriteLine($"Disposable source: {sourceRoot}");
        Console.WriteLine($"Disposable destination: {destinationRoot}");
        var settings = new AppSettings { DownloadsPath = sourceRoot };
        var store = new SqliteStateStore(Path.Combine(sourceRoot, "acceptance.db"));
        store.SaveSettings(settings);
        DownloadOrganizer organizer = CreateOrganizer(store);
        var mover = new SafeFileMover();

        TrackedFile normal = CreateReviewFile(store, sourceRoot, "complete.bin", 64);
        await organizer.MoveAsync(normal.Id, settings, destinationRoot);
        string normalDestination = Path.Combine(destinationRoot, normal.Name);
        Require(!File.Exists(normal.CurrentPath) && mover.Hash(normalDestination) == normal.ContentHash, "Cross-volume move did not preserve content.");
        await organizer.UndoAsync(store.GetOperations().Last().Id, settings);
        Require(File.Exists(normal.CurrentPath) && !File.Exists(normalDestination) && mover.Hash(normal.CurrentPath) == normal.ContentHash,
            "Cross-volume undo did not restore the original content.");

        TrackedFile interrupted = CreateReviewFile(store, sourceRoot, "interrupted.bin", 512);
        string interruptedDestination = Path.Combine(destinationRoot, interrupted.Name);
        using Process child = StartMoveWorker(interrupted, destinationRoot, sourceRoot);
        var timer = Stopwatch.StartNew();
        try
        {
            while (!File.Exists(interruptedDestination) && !child.HasExited && timer.Elapsed < TimeSpan.FromSeconds(60))
            {
                await Task.Delay(1);
            }
            Require(!child.HasExited && File.Exists(interruptedDestination) && File.Exists(interrupted.CurrentPath),
                "Could not observe a copy in progress; interruption acceptance is inconclusive.");
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync();
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }
        }

        Require(File.Exists(interrupted.CurrentPath) && File.Exists(interruptedDestination), "The interruption did not leave two inspectable paths.");
        string sourceHash = mover.Hash(interrupted.CurrentPath);
        string destinationHash = mover.Hash(interruptedDestination);
        Require(sourceHash == interrupted.ContentHash, "The interrupted move damaged the source.");
        Require(destinationHash != sourceHash, "The copy completed before interruption; repeat the test with a larger file.");
        OperationRecord pending = store.GetOperations().Last();
        Require(pending.Status == OperationStatus.Pending, "The child did not leave a pending journal entry.");
        await organizer.RetryRecoveryAsync();
        Require(store.GetOperations().Last().Status == OperationStatus.NeedsAttention, "Ambiguous copies were not flagged for inspection.");
        Require(mover.Hash(interrupted.CurrentPath) == sourceHash && mover.Hash(interruptedDestination) == destinationHash,
            "Recovery changed one of the preserved copies.");
        bool blocked = false;
        try
        {
            await organizer.MoveAsync(interrupted.Id, settings, destinationRoot);
        }
        catch (IOException) { blocked = true; }
        Require(blocked && store.GetOperations().Count == 3, "An unresolved operation did not block another move.");

        var report = new
        {
            Passed = true,
            RecordedAtUtc = DateTimeOffset.UtcNow,
            OperatingSystem = Environment.OSVersion.VersionString,
            SourceRoot = sourceRoot,
            DestinationRoot = destinationRoot,
            CompletedMoveAndUndoMiB = 64,
            InterruptedMoveMiB = 512,
            SourcePreserved = true,
            PartialDestinationPreserved = true,
            RecoveryStatus = "NeedsAttention",
            FurtherMoveBlocked = true,
            Note = "Fixtures are retained for inspection. This does not test power loss, disk failure, or Recycle Bin policy."
        };
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Acceptance passed. Report: {reportPath}");
    }

    private static TrackedFile CreateReviewFile(SqliteStateStore store, string root, string name, int sizeMiB)
    {
        string path = Path.Combine(root, AppSettings.ReviewFolderName, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] block = new byte[1024 * 1024];
        Random.Shared.NextBytes(block);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            for (int index = 0; index < sizeMiB; index++)
            {
                stream.Write(block);
            }
            stream.Flush(flushToDisk: true);
        }
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-1));
        var file = new TrackedFile(Guid.NewGuid(), Path.Combine(root, name), path, FileCategory.Other,
            DateTimeOffset.UtcNow.AddDays(-31), LifecycleStatus.InReview, new SafeFileMover().Hash(path));
        store.SaveFile(file);
        return file;
    }

    private static Process StartMoveWorker(TrackedFile file, string destination, string sourceRoot)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(start.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        }
        start.ArgumentList.Add("move");
        start.ArgumentList.Add(Path.Combine(sourceRoot, "acceptance.db"));
        start.ArgumentList.Add(file.Id.ToString());
        start.ArgumentList.Add(destination);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start the isolated move worker.");
    }

    private static DownloadOrganizer CreateOrganizer(SqliteStateStore store) =>
        new(store, new SafeFileMover(), new FileReadiness(TimeSpan.Zero, TimeSpan.Zero), new RefuseRecycle());

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RefuseRecycle : IRecycleBin
    {
        public Task RecycleAsync(string path) => throw new InvalidOperationException("This harness does not recycle files.");
    }
}
