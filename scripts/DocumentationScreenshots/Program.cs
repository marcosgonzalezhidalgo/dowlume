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

namespace DocumentationScreenshots;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Render documentation screenshots on Windows.");
        }

        string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/documentation-screenshots");
        Directory.CreateDirectory(output);
        // Only this randomly created profile is written; displayed download paths are fictional.
        string profile = Path.Combine(Path.GetTempPath(), "Dowlume.Documentation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(CaptureAppBuilder));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            session.Dispatch(async () =>
            {
                var settings = new AppSettings { DownloadsPath = @"C:\Dowlume-Demo\Downloads" };
                var store = new SqliteStateStore(Path.Combine(profile, "state.db"));
                store.SaveSettings(settings);
                string[] names = ["Annual report.pdf", "Coast photo.jpg", "Demo recording.mp4", "Design assets.zip", "Editor setup.msi", "Project notes.txt"];
                foreach (string name in names)
                {
                    CategoryRule rule = FileClassifier.Classify(name, settings);
                    bool review = rule.Category is FileCategory.Archives or FileCategory.Installers;
                    store.SaveFile(new TrackedFile(Guid.NewGuid(), Path.Combine(settings.DownloadsPath, name),
                        Path.Combine(settings.DownloadsPath, review ? AppSettings.ReviewFolderName : rule.FolderName, name),
                        rule.Category, new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
                        review ? LifecycleStatus.InReview : LifecycleStatus.Organized, "demo-content-hash"));
                }

                var organizer = new DownloadOrganizer(store, new SafeFileMover(), new FileReadiness(), new NoRecycle());
                var model = new MainViewModel(store, organizer, new NoDialogs(), new DiagnosticLog(Path.Combine(profile, "diagnostics.jsonl")));
                var window = new MainWindow { DataContext = model };
                window.Show();
                try
                {
                    await model.RefreshAsync();
                    model.StatusMessage = "Monitoring paused. Preview again when you are ready to resume.";
                    TabControl tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
                    Capture(0, "overview.png");

                    // Populate the real Preview template without invoking filesystem commands.
                    foreach (string name in names)
                    {
                        CategoryRule rule = FileClassifier.Classify(name, settings);
                        model.PreviewItems.Add(new PreviewItem(Path.Combine(settings.DownloadsPath, name),
                            Path.Combine(settings.DownloadsPath, rule.FolderName, name), rule.Category, "demo-content-hash"));
                    }
                    model.HasPreview = true;
                    model.StatusMessage = "6 completed files are ready. Busy, recent, excluded, and already tracked files are skipped. Nothing has moved.";
                    Capture(1, "classification.png");

                    model.SelectedReview = model.ReviewFiles[0];
                    model.StatusMessage = "Monitoring paused. Preview again when you are ready to resume.";
                    Capture(2, "review.png");

                    void Capture(int index, string name)
                    {
                        tabs.SelectedIndex = index;
                        window.UpdateLayout();
                        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
                        frame.Save(Path.Combine(output, name), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    }
                }
                finally { window.Close(); }
                return true;
            }, timeout.Token).GetAwaiter().GetResult();
        }
        finally { Directory.Delete(profile, recursive: true); }
        Console.WriteLine($"Documentation screenshots saved to {output}");
    }

    public sealed class CaptureAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CaptureApplication>()
            .WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    public sealed class CaptureApplication : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }

    private sealed class NoDialogs : IDesktopDialogs
    {
        public Task<string?> ChooseFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmAsync(string title, string message, string action) => Task.FromResult(false);
        public void RevealFile(string path) => throw new InvalidOperationException("Documentation rendering cannot open files.");
    }

    private sealed class NoRecycle : IRecycleBin
    {
        public Task RecycleAsync(string path) => throw new InvalidOperationException("Documentation rendering cannot recycle files.");
    }
}
