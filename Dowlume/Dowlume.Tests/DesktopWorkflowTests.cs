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
        public Task RecycleAsync(string path) => throw new InvalidOperationException("This test does not recycle files.");
    }
}
