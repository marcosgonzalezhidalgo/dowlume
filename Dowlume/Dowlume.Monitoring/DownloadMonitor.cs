using System.Collections.Concurrent;
using System.Threading.Channels;
using Dowlume.Core;

namespace Dowlume.Monitoring;

public sealed class DownloadMonitor : IAsyncDisposable
{
    private readonly AppSettings _settings;
    private readonly Func<string, CancellationToken, Task> _process;
    private readonly Func<CancellationToken, Task> _reconcile;
    private readonly Action<Exception> _reportError;
    private readonly TimeSpan _scanInterval;
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, byte> _queued = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly FileSystemWatcher _watcher;
    private readonly Task _worker;
    private readonly Task _scanner;

    public DownloadMonitor(AppSettings settings, Func<string, CancellationToken, Task> process,
        Func<CancellationToken, Task> reconcile, Action<Exception> reportError,
        TimeSpan? scanInterval = null, int capacity = 256)
    {
        settings.Validate();
        _settings = settings;
        _process = process;
        _reconcile = reconcile;
        _reportError = reportError;
        _scanInterval = scanInterval ?? TimeSpan.FromSeconds(15);
        _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _watcher = new FileSystemWatcher(settings.DownloadsPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.Error += (_, args) => _reportError(args.GetException());
        _watcher.EnableRaisingEvents = true;
        _worker = Task.Run(ProcessQueueAsync);
        _scanner = Task.Run(ScanPeriodicallyAsync);
    }

    private void OnChanged(object sender, FileSystemEventArgs args) => Enqueue(args.FullPath);

    private void Enqueue(string path)
    {
        if (FileClassifier.IsExcluded(path, _settings) || !_queued.TryAdd(path, 0))
        {
            return;
        }
        if (!_queue.Writer.TryWrite(path))
        {
            // Reconciliation will rediscover items that did not fit in the bounded queue.
            _queued.TryRemove(path, out _);
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (string path in _queue.Reader.ReadAllAsync(_shutdown.Token))
            {
                try
                {
                    await _process(path, _shutdown.Token);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
                catch (Exception error) { _reportError(error); }
                finally { _queued.TryRemove(path, out _); }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    private async Task ScanPeriodicallyAsync()
    {
        using var timer = new PeriodicTimer(_scanInterval);
        try
        {
            do
            {
                try
                {
                    await _reconcile(_shutdown.Token);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
                catch (Exception error) { _reportError(error); }
                try
                {
                    // Re-enable after transient watcher errors; scanning also works without events.
                    if (!_watcher.EnableRaisingEvents)
                    {
                        _watcher.EnableRaisingEvents = true;
                    }
                    foreach (string path in Directory.EnumerateFiles(_settings.DownloadsPath))
                    {
                        _shutdown.Token.ThrowIfCancellationRequested();
                        Enqueue(path);
                    }
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
                catch (Exception error) { _reportError(error); }
            }
            while (await timer.WaitForNextTickAsync(_shutdown.Token));
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _watcher.Dispose();
        await _shutdown.CancelAsync();
        _queue.Writer.TryComplete();
        await Task.WhenAll(_worker, _scanner);
        _shutdown.Dispose();
    }
}
