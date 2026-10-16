using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Dowlume.App.Services;
using Dowlume.App.ViewModels;
using Dowlume.App.Views;
using Dowlume.Core;
using Dowlume.Infrastructure;
using Dowlume.Monitoring;

namespace Dowlume.Tests;

public sealed class DesktopWorkflowTests
{
    [Fact]
    public async Task TrayRemainsAttachedWhileHiddenAndBothOpenActionsRestoreTheWindow()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(() =>
        {
            var window = new MainWindow();
            window.Show();
            using (var tray = new DesktopTray(Application.Current!, window))
            {
                TrayIcon icon = Assert.Single(TrayIcon.GetIcons(Application.Current!)!);
                Assert.True(icon.IsVisible);
                window.Hide();
                Assert.False(window.IsVisible);
                icon.Command!.Execute(null);
                Assert.True(window.IsVisible);
                window.WindowState = WindowState.Minimized;
                window.Hide();
                var open = Assert.IsType<NativeMenuItem>(icon.Menu!.Items[0]);
                open.Command!.Execute(null);
                Assert.True(window.IsVisible);
                Assert.Equal(WindowState.Normal, window.WindowState);
                var exit = Assert.IsType<NativeMenuItem>(icon.Menu.Items[2]);
                exit.Command!.Execute(null);
                Assert.False(window.IsVisible);
            }
            Assert.Null(TrayIcon.GetIcons(Application.Current!));
            return true;
        }, timeout.Token);
    }

    [Fact]
    public async Task StartupToggleIsOptInAndReportsFailuresWithoutClaimingSuccess()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            using var workspace = new TemporaryWorkspace();
            var store = new SqliteStateStore(workspace.Database);
            var organizer = new DownloadOrganizer(store, new SafeFileMover(), new FileReadiness(), new NoRecycle());
            var startup = new TestStartup();
            var model = new MainViewModel(store, organizer, new TestDialogs(),
                new DiagnosticLog(Path.Combine(workspace.Root, "diagnostics.jsonl")), startup);
            Assert.False(model.StartWithWindows);
            Assert.Equal(0, startup.Changes);
            await model.ToggleWindowsStartupCommand.ExecuteAsync(null);
            Assert.True(model.StartWithWindows);
            Assert.False(model.IsMonitoring);
            startup.Fail = true;
            await model.ToggleWindowsStartupCommand.ExecuteAsync(null);
            Assert.True(model.StartWithWindows);
            Assert.Contains("Access denied", model.ErrorMessage);
            startup.Fail = false;
            await model.ToggleWindowsStartupCommand.ExecuteAsync(null);
            Assert.False(model.StartWithWindows);
            await model.ShutdownAsync();
            return true;
        }, timeout.Token);
    }

    [Fact]
    public async Task ActivitySearchPagingAndUndoWorkWithOlderHistory()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            using var workspace = new TemporaryWorkspace();
            var settings = new AppSettings { DownloadsPath = workspace.Downloads };
            var store = new SqliteStateStore(workspace.Database);
            store.SaveSettings(settings);
            var organizer = new DownloadOrganizer(store, new SafeFileMover(), new FileReadiness(TimeSpan.Zero, TimeSpan.Zero), new NoRecycle());
            string source = workspace.CreateFile("Older report.pdf");
            await organizer.OrganizeAsync(source, settings);
            OperationRecord original = Assert.Single(store.GetOperations());
            for (int index = 0; index < 205; index++)
            {
                store.SaveOperation(original with
                {
                    Id = Guid.NewGuid(),
                    SourcePath = Path.Combine(workspace.Downloads, $"other-{index}.pdf"),
                    Status = OperationStatus.Failed,
                    Error = "Disposable history fixture."
                });
            }
            var dialogs = new TestDialogs { Confirm = true };
            var model = new MainViewModel(store, organizer, dialogs, new DiagnosticLog(Path.Combine(workspace.Root, "diagnostics.jsonl")));
            var window = new MainWindow { DataContext = model };
            window.Show();
            try
            {
                await model.RefreshAsync();
                Assert.Equal(50, model.Activity.Count);
                Assert.True(model.HasNextActivityPage);
                Guid firstPageId = model.Activity[0].Id;
                await model.NextActivityPageCommand.ExecuteAsync(null);
                Assert.True(model.HasPreviousActivityPage);
                Assert.DoesNotContain(model.Activity, operation => operation.Id == firstPageId);
                model.SelectedOperation = model.Activity[0];
                Guid olderPageId = model.SelectedOperation.Id;
                store.SaveOperation(original with
                {
                    Id = Guid.NewGuid(),
                    Status = OperationStatus.Failed
                });
                await model.RefreshAsync();
                Assert.Equal(olderPageId, model.SelectedOperation?.Id);
                Assert.Equal(olderPageId, model.Activity[0].Id);

                model.ActivitySearch = "older REPORT";
                model.SelectedActivityKind = MainViewModel.ActivityKinds.Single(filter => filter.Value == OperationKind.Organize);
                model.SelectedActivityStatus = MainViewModel.ActivityStatuses.Single(filter => filter.Value == OperationStatus.Completed);
                await model.SearchActivityCommand.ExecuteAsync(null);
                Assert.Equal(original.Id, Assert.Single(model.Activity).Id);
                Assert.False(model.HasPreviousActivityPage);
                Assert.False(model.HasNextActivityPage);
                Assert.Null(model.SelectedOperation);
                model.SelectedOperation = model.Activity[0];
                await model.UndoCommand.ExecuteAsync(null);
                Assert.True(File.Exists(source), model.ErrorMessage);
                Assert.Equal(LifecycleStatus.Kept, Assert.Single(store.GetFiles()).Status);

                model.ActivitySearch = "no such file";
                await model.SearchActivityCommand.ExecuteAsync(null);
                Assert.True(model.IsActivityEmpty);
                Assert.Null(model.SelectedOperation);
                await model.ClearActivityFiltersCommand.ExecuteAsync(null);
                Assert.Equal(50, model.Activity.Count);
                Assert.False(model.IsActivityEmpty);
                Assert.False(model.HasPreviousActivityPage);
                Assert.Equal(string.Empty, model.ErrorMessage);

                TabControl tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
                tabs.SelectedIndex = 3;
                window.Width = window.MinWidth;
                window.Height = window.MinHeight;
                window.UpdateLayout();
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Assert.Equal(2, window.GetVisualDescendants().OfType<ComboBox>().Count(combo => combo.IsEffectivelyVisible));
            }
            finally { window.Close(); }
            return true;
        }, timeout.Token);
    }

    [Fact]
    public async Task PreviewConsentMonitoringAndReviewWorkThroughTheDesktop()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            using var workspace = new TemporaryWorkspace();
            var settings = new AppSettings { DownloadsPath = workspace.Downloads };
            var store = new SqliteStateStore(workspace.Database);
            store.SaveSettings(settings);
            var clock = new TestClock();
            var organizer = new DownloadOrganizer(store, new SafeFileMover(), new FileReadiness(TimeSpan.FromMilliseconds(5), TimeSpan.Zero), new NoRecycle(), clock);
            var dialogs = new TestDialogs();
            var model = new MainViewModel(store, organizer, dialogs, new DiagnosticLog(Path.Combine(workspace.Root, "diagnostics.jsonl")));
            var window = new MainWindow { DataContext = model };
            window.Show();
            string source = workspace.CreateFile("A sample report.pdf");

            await model.PreviewCommand.ExecuteAsync(null);
            Assert.Single(model.PreviewItems);
            Assert.True(File.Exists(source));
            Assert.False(model.IsMonitoring);

            await model.StartCommand.ExecuteAsync(null);
            Assert.False(model.IsMonitoring);
            Assert.True(File.Exists(source));

            dialogs.Confirm = true;
            await model.StartCommand.ExecuteAsync(null);
            Assert.True(model.IsMonitoring, model.ErrorMessage);
            await model.PauseCommand.ExecuteAsync(null);
            Assert.False(model.IsMonitoring);
            clock.Now = clock.Now.AddDays(31);
            await organizer.ReconcileAsync(settings);
            await model.RefreshAsync();
            model.SelectedReview = Assert.Single(model.ReviewFiles);
            await model.KeepCommand.ExecuteAsync(null);
            Assert.Empty(model.ReviewFiles);
            Assert.Equal(LifecycleStatus.Kept, Assert.Single(model.ManagedFiles).Status);

            TabControl tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            for (int index = 0; index < 5; index++)
            {
                tabs.SelectedIndex = index;
                window.UpdateLayout();
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                string? captureDirectory = Environment.GetEnvironmentVariable("DOWLUME_TEST_SCREENSHOTS");
                if (captureDirectory is not null)
                {
                    Directory.CreateDirectory(captureDirectory);
                    frame.Save(Path.Combine(captureDirectory, $"screen-{index}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
            }
            window.Close();
            return true;
        }, timeout.Token);
    }

    public sealed class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
            .WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    public sealed class TestApplication : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestDialogs : IDesktopDialogs
    {
        public bool Confirm
        {
            get; set;
        }
        public Task<string?> ChooseFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmAsync(string title, string message, string action) => Task.FromResult(Confirm);
        public void RevealFile(string path)
        {
        }
    }

    private sealed class NoRecycle : IRecycleBin
    {
        public Task RecycleAsync(string path, string expectedHash) => throw new InvalidOperationException("This test does not recycle files.");
    }

    private sealed class TestStartup : IStartupRegistration
    {
        public bool IsSupported => true;
        public bool IsEnabled
        {
            get; private set;
        }
        public bool Fail
        {
            get; set;
        }
        public int Changes
        {
            get; private set;
        }

        public void SetEnabled(bool enabled)
        {
            if (Fail)
            {
                throw new UnauthorizedAccessException();
            }
            Changes++;
            IsEnabled = enabled;
        }
    }
}
