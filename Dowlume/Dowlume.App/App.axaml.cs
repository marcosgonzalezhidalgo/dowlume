using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dowlume.App.Services;
using Dowlume.App.ViewModels;
using Dowlume.App.Views;
using Dowlume.Infrastructure;
using Dowlume.Monitoring;

namespace Dowlume.App;

public partial class App : Application
{
    private FileStream? _instanceLock;
    private DesktopTray? _tray;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string dataDirectory = Program.DataDirectory;
            var log = new DiagnosticLog(Path.Combine(dataDirectory, "diagnostics.jsonl"));
            try
            {
                Directory.CreateDirectory(dataDirectory);
                _instanceLock = new FileStream(Path.Combine(dataDirectory, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var store = new SqliteStateStore(Path.Combine(dataDirectory, "dowlume.db"));
                var organizer = new DownloadOrganizer(store, new SafeFileMover(), new FileReadiness(), new WindowsRecycleBin());
                var window = new MainWindow();
                window.DataContext = new MainViewModel(store, organizer, new DesktopDialogs(window), log,
                    new WindowsStartupRegistration());
                desktop.MainWindow = window;
                _tray = new DesktopTray(this, window);
                log.Write("application_started");
            }
            catch (Exception error)
            {
                log.Write("startup_failed", error);
                desktop.MainWindow = new Window
                {
                    Title = "Dowlume — unable to start",
                    Width = 520,
                    Height = 220,
                    Content = new TextBlock
                    {
                        Margin = new Thickness(28),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Text = "Dowlume could not open its local data. Another instance may already be running. Close it and try again. If the problem persists, back up your local database before repairing or reinstalling."
                    }
                };
            }
            desktop.Exit += (_, _) => { _tray?.Dispose(); _instanceLock?.Dispose(); };
        }

        base.OnFrameworkInitializationCompleted();
    }

}
