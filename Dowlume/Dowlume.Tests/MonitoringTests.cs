using System.Collections.Concurrent;
using Dowlume.Core;
using Dowlume.Monitoring;

namespace Dowlume.Tests;

public sealed class MonitoringTests
{
    [Fact]
    public async Task LockedFileIsNotReady()
    {
        using var workspace = new TemporaryWorkspace();
        string path = workspace.CreateFile("locked.pdf");
        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        var readiness = new FileReadiness(TimeSpan.FromMilliseconds(10), TimeSpan.Zero);
        Assert.False(await readiness.IsReadyAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task ChangedFileIsNotReady()
    {
        using var workspace = new TemporaryWorkspace();
        string path = workspace.CreateFile("changing.pdf");
        var readiness = new FileReadiness(TimeSpan.FromMilliseconds(300), TimeSpan.Zero);
        Task<bool> ready = readiness.IsReadyAsync(path, CancellationToken.None);
        await File.AppendAllTextAsync(path, "More content");
        Assert.False(await ready);
        Assert.True(await readiness.IsReadyAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task FullQueueEventuallyDiscoversEveryFileAndIgnoresTemporaryFiles()
    {
        using var workspace = new TemporaryWorkspace();
        var seen = new ConcurrentDictionary<string, byte>();
        var errors = new ConcurrentQueue<Exception>();
        for (int index = 0; index < 40; index++)
        {
            workspace.CreateFile($"file-{index}.pdf");
        }
        workspace.CreateFile("incomplete.crdownload");
        var settings = new AppSettings { DownloadsPath = workspace.Downloads };
        await using var monitor = new DownloadMonitor(settings, async (path, token) =>
        {
            await Task.Delay(5, token);
            seen.TryAdd(path, 0);
            File.Move(path, Path.Combine(workspace.Root, Path.GetFileName(path)));
        }, _ => Task.CompletedTask, errors.Enqueue, TimeSpan.FromMilliseconds(30), capacity: 3);
        await WaitUntilAsync(() => seen.Count == 40);
        Assert.Empty(errors);
        Assert.True(File.Exists(Path.Combine(workspace.Downloads, "incomplete.crdownload")));
    }

    [Fact]
    public async Task RenameAndRestartRecoverDownloads()
    {
        using var workspace = new TemporaryWorkspace();
        string temporary = workspace.CreateFile("file.crdownload");
        var settings = new AppSettings { DownloadsPath = workspace.Downloads };
        var seen = new ConcurrentDictionary<string, byte>();
        Task Process(string path, CancellationToken token)
        {
            seen.TryAdd(path, 0);
            return Task.CompletedTask;
        }
        await using (var first = new DownloadMonitor(settings, Process, _ => Task.CompletedTask, _ => { }, TimeSpan.FromMilliseconds(30)))
        {
            File.Move(temporary, Path.ChangeExtension(temporary, ".pdf"));
            await WaitUntilAsync(() => seen.Count == 1);
        }
        workspace.CreateFile("offline.pdf");
        await using var second = new DownloadMonitor(settings, Process, _ => Task.CompletedTask, _ => { }, TimeSpan.FromMilliseconds(30));
        await WaitUntilAsync(() => seen.Count == 2);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
