using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dowlume.App.Services;
using Dowlume.Core;
using Dowlume.Infrastructure;
using Dowlume.Monitoring;

namespace Dowlume.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStateStore _store;
    private readonly DownloadOrganizer _organizer;
    private readonly IDesktopDialogs _dialogs;
    private readonly DiagnosticLog _log;
    private readonly IStartupRegistration? _startup;
    private readonly CancellationTokenSource _shutdown = new();
    private DownloadMonitor? _monitor;
    private AppSettings? _previewSettings;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private Task _activeWork = Task.CompletedTask;

    [ObservableProperty] private string _downloadsPath = string.Empty;
    [ObservableProperty] private string _excludedExtensions = string.Empty;
    [ObservableProperty] private string _statusMessage = "Choose a folder, preview the changes, then start organizing.";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private int _managedCount;
    [ObservableProperty] private int _reviewCount;
    [ObservableProperty] private int _attentionCount;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanReviewSelectedFile))] private TrackedFile? _selectedReview;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanUndoSelectedOperation))] private OperationRecord? _selectedOperation;

    public bool IsIdle => !IsBusy;
    public bool SupportsWindowsStartup => _startup?.IsSupported == true;
    public bool CanReviewSelectedFile => IsIdle && SelectedReview is not null;
    public bool CanUndoSelectedOperation => IsIdle && SelectedOperation?.CanUndo == true;
    public bool CanConfigure => !IsBusy && !IsMonitoring;
    public string MonitoringLabel => IsMonitoring ? "Monitoring is on" : "Monitoring is paused";
    public ObservableCollection<CategoryRuleViewModel> Rules { get; } = [];
    public ObservableCollection<PreviewItem> PreviewItems { get; } = [];
    public ObservableCollection<TrackedFile> ReviewFiles { get; } = [];
    public ObservableCollection<TrackedFile> ManagedFiles { get; } = [];
    public ObservableCollection<OperationRecord> Activity { get; } = [];
    public ObservableCollection<CategorySummary> Categories { get; } = [];

    public MainViewModel(IStateStore store, DownloadOrganizer organizer, IDesktopDialogs dialogs, DiagnosticLog log,
        IStartupRegistration? startup = null)
    {
        _store = store;
        _organizer = organizer;
        _dialogs = dialogs;
        _log = log;
        _startup = startup;
        try
        {
            StartWithWindows = startup?.IsEnabled == true;
        }
        catch (Exception error) { ShowError(error); }
        AppSettings settings = store.LoadSettings() ?? new AppSettings { DownloadsPath = DownloadsFolder.Suggest() };
        DownloadsPath = settings.DownloadsPath;
        ExcludedExtensions = string.Join(", ", settings.ExcludedExtensions);
        foreach (CategoryRule rule in settings.Categories)
        {
            Rules.Add(new CategoryRuleViewModel(rule));
        }
    }

    [RelayCommand]
    private Task ToggleWindowsStartupAsync() => RunAsync(() =>
    {
        if (_startup is null || !_startup.IsSupported)
        {
            throw new InvalidOperationException("Windows startup is unavailable for this application host.");
        }
        try
        {
            _startup.SetEnabled(!StartWithWindows);
            StartWithWindows = _startup.IsEnabled;
            StatusMessage = StartWithWindows
                ? "Dowlume will open when you sign in. Monitoring will remain paused until you preview and start."
                : "Dowlume will no longer start automatically from this location.";
        }
        finally
        {
            // Restore the checkbox if Windows refused the requested change.
            OnPropertyChanged(nameof(StartWithWindows));
        }
        return Task.CompletedTask;
    });

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CanReviewSelectedFile));
        OnPropertyChanged(nameof(CanUndoSelectedOperation));
        OnPropertyChanged(nameof(CanConfigure));
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConfigure));
        OnPropertyChanged(nameof(MonitoringLabel));
    }

    [RelayCommand]
    private Task ChooseFolderAsync() => RunAsync(async () =>
    {
        string? folder = await _dialogs.ChooseFolderAsync("Choose your Downloads folder");
        if (folder is not null)
        {
            DownloadsPath = folder;
            InvalidatePreview();
        }
    });

    [RelayCommand]
    private Task SaveSettingsAsync() => RunAsync(() =>
    {
        _store.SaveSettings(ReadSettings());
        InvalidatePreview();
        StatusMessage = "Settings saved. Preview the folder before starting monitoring.";
        return Task.CompletedTask;
    });

    [RelayCommand]
    private Task PreviewAsync() => RunAsync(async () =>
    {
        AppSettings settings = ReadSettings();
        StatusMessage = "Checking completed downloads. Large files may take a moment.";
        IReadOnlyList<PreviewItem> items = await Task.Run(() => _organizer.PreviewAsync(settings, _shutdown.Token));
        Replace(PreviewItems, items);
        _previewSettings = settings;
        HasPreview = true;
        StatusMessage = $"{items.Count} completed files are ready. Busy, recent, excluded, and already tracked files are skipped. Nothing has moved.";
    });

    [RelayCommand]
    private Task StartAsync() => RunAsync(async () =>
    {
        AppSettings settings = ReadSettings();
        if (_previewSettings is null || JsonSerializer.Serialize(settings) != JsonSerializer.Serialize(_previewSettings))
        {
            throw new InvalidOperationException("Preview the current settings before starting.");
        }
        if (!await _dialogs.ConfirmAsync("Start organizing downloads?",
                $"Organize {PreviewItems.Count} previewed files and monitor this folder for completed downloads?\n\n{settings.DownloadsPath}\n\nFuture and currently unfinished downloads will also be organized. Aging files will move to Expiring Files for your review. No files will be deleted automatically.", "Organize and monitor"))
        {
            return;
        }
        _store.SaveSettings(settings);
        foreach (PreviewItem item in PreviewItems.ToArray())
        {
            await Task.Run(() => _organizer.OrganizeAsync(item.SourcePath, settings, _shutdown.Token, item.ContentHash));
        }
        _shutdown.Token.ThrowIfCancellationRequested();
        _monitor = new DownloadMonitor(settings,
            (path, token) => _organizer.OrganizeAsync(path, settings, token),
            token => _organizer.ReconcileAsync(settings, token), ReportBackgroundError);
        IsMonitoring = true;
        InvalidatePreview();
        StatusMessage = "Monitoring completed downloads. You can pause at any time.";
        await RefreshAsync();
    });

    [RelayCommand]
    private Task PauseAsync() => RunAsync(async () =>
    {
        await StopMonitorAsync();
        StatusMessage = "Monitoring paused. Preview again when you are ready to resume.";
    });

    [RelayCommand]
    private Task KeepAsync() => ReviewActionAsync((file, settings) => _organizer.KeepAsync(file.Id, settings, _shutdown.Token));

    [RelayCommand]
    private Task SnoozeAsync() => ReviewActionAsync((file, settings) => _organizer.SnoozeAsync(file.Id, settings, _shutdown.Token));

    [RelayCommand]
    private Task MoveAsync() => RunAsync(async () =>
    {
        TrackedFile file = RequireReviewSelection();
        string? folder = await _dialogs.ChooseFolderAsync("Choose where to move this file");
        if (folder is null)
        {
            return;
        }
        if (!await _dialogs.ConfirmAsync("Move file?", $"Move {file.Name} to:\n\n{folder}\n\nDowlume will stop monitoring this file. Existing files will not be overwritten.", "Move file"))
        {
            return;
        }
        await Task.Run(() => _organizer.MoveAsync(file.Id, SavedSettings(), folder, _shutdown.Token));
        await RefreshAsync();
    });

    [RelayCommand]
    private Task RecycleAsync() => RunAsync(async () =>
    {
        TrackedFile file = RequireReviewSelection();
        if (!await _dialogs.ConfirmAsync("Send to Recycle Bin?", $"Recycle {file.Name}?\n\nRestore it through Windows Recycle Bin if needed. This action cannot be undone inside Dowlume.", "Send to Recycle Bin"))
        {
            return;
        }
        await Task.Run(() => _organizer.RecycleAsync(file.Id, SavedSettings(), _shutdown.Token));
        await RefreshAsync();
    });

    [RelayCommand]
    private Task UndoAsync() => RunAsync(async () =>
    {
        OperationRecord operation = SelectedOperation ?? throw new InvalidOperationException("Select an action in Activity first.");
        if (!await _dialogs.ConfirmAsync("Undo this action?", $"Undo {operation.Description}?\n\nThe original location must be available and the file must be unchanged. Undoing initial organization keeps the file in place permanently. Undoing a review move snoozes the file for seven days.", "Undo action"))
        {
            return;
        }
        await Task.Run(() => _organizer.UndoAsync(operation.Id, SavedSettings(), _shutdown.Token));
        await RefreshAsync();
    });

    [RelayCommand]
    private Task RetryRecoveryAsync() => RunAsync(async () =>
    {
        await Task.Run(() => _organizer.RetryRecoveryAsync(_shutdown.Token));
        await RefreshAsync();
        StatusMessage = "Recovery checked. Inspect any NeedsAttention entries in Activity; both paths are shown there.";
    });

    [RelayCommand]
    private Task RevealReviewAsync() => RunAsync(() =>
    {
        _dialogs.RevealFile(RequireReviewSelection().CurrentPath);
        return Task.CompletedTask;
    });

    private Task ReviewActionAsync(Func<TrackedFile, AppSettings, Task> action) => RunAsync(async () =>
    {
        TrackedFile file = RequireReviewSelection();
        AppSettings settings = SavedSettings();
        await Task.Run(() => action(file, settings));
        await RefreshAsync();
    });

    public async Task RefreshAsync(bool skipIfRefreshing = false)
    {
        if (_shutdown.IsCancellationRequested)
        {
            return;
        }
        if (!await _refreshGate.WaitAsync(skipIfRefreshing ? 0 : Timeout.Infinite))
        {
            return;
        }
        try
        {
            if (_shutdown.IsCancellationRequested)
            {
                return;
            }
            OperationHistoryQuery query = _historyQuery;
            var snapshot = await Task.Run(() => (Files: _store.GetFiles(), History: _store.GetOperationHistory(query),
                AttentionCount: _store.CountOperationsRequiringAttention()));
            TrackedFile? selectedReview = SelectedReview;
            string root = _store.LoadSettings()?.DownloadsPath ?? DownloadsPath;
            TrackedFile[] files = snapshot.Files.Where(file => PathSafety.IsWithin(root, file.CurrentPath)).ToArray();
            Replace(ReviewFiles, files.Where(file => file.Status == LifecycleStatus.InReview));
            Replace(ManagedFiles, files.Where(file => file.IsManaged || file.Status == LifecycleStatus.Missing));
            if (query == _historyQuery)
            {
                ShowActivityPage(snapshot.History);
            }
            Replace(Categories, Enum.GetValues<FileCategory>().Select(category => new CategorySummary(category, files.Count(file => file.IsManaged && file.Category == category))));
            SelectedReview = ReviewFiles.FirstOrDefault(file => file.Id == selectedReview?.Id);
            ManagedCount = files.Count(file => file.IsManaged);
            ReviewCount = ReviewFiles.Count;
            AttentionCount = files.Count(file => file.Status == LifecycleStatus.Missing) +
                snapshot.AttentionCount;
        }
        catch (Exception error) { ShowError(error); }
        finally { _refreshGate.Release(); }
    }

    private AppSettings ReadSettings()
    {
        if (IsMonitoring)
        {
            throw new InvalidOperationException("Pause monitoring before changing settings or previewing files.");
        }
        if (string.IsNullOrWhiteSpace(DownloadsPath) || !Path.IsPathFullyQualified(DownloadsPath.Trim()))
        {
            throw new ArgumentException("Choose an absolute Downloads folder path.");
        }
        var settings = new AppSettings
        {
            DownloadsPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(DownloadsPath.Trim())),
            Categories = Rules.Select(rule => rule.ToRule()).ToList(),
            ExcludedExtensions = CategoryRuleViewModel.ParseExtensions(ExcludedExtensions)
        };
        settings.Validate();
        PathSafety.ValidateRoot(settings.DownloadsPath);
        return settings;
    }

    private AppSettings SavedSettings() => _store.LoadSettings() ?? throw new InvalidOperationException("Save and preview a Downloads folder first.");
    private TrackedFile RequireReviewSelection() => SelectedReview ?? throw new InvalidOperationException("Select a file in the review list first.");

    private Task RunAsync(Func<Task> action)
    {
        if (IsBusy || _shutdown.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        _activeWork = RunGuardedAsync(action);
        return _activeWork;
    }

    private async Task RunGuardedAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception error) { ShowError(error); }
        finally { IsBusy = false; }
    }

    private void ShowError(Exception error)
    {
        _log.Write("operation_failed", error);
        ErrorMessage = DownloadOrganizer.FriendlyError(error);
    }

    private void ReportBackgroundError(Exception error) => Dispatcher.UIThread.Post(() => ShowError(error));

    private void InvalidatePreview()
    {
        _previewSettings = null;
        HasPreview = false;
        PreviewItems.Clear();
    }

    private async Task StopMonitorAsync()
    {
        if (_monitor is not null)
        {
            await _monitor.DisposeAsync();
            _monitor = null;
        }
        IsMonitoring = false;
    }

    public async Task ShutdownAsync()
    {
        await _shutdown.CancelAsync();
        await _activeWork;
        await StopMonitorAsync();
        await _refreshGate.WaitAsync();
        _refreshGate.Release();
        _shutdown.Dispose();
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        T[] snapshot = values.ToArray();
        if (target.SequenceEqual(snapshot))
        {
            return;
        }
        target.Clear();
        foreach (T item in snapshot)
        {
            target.Add(item);
        }
    }
}
