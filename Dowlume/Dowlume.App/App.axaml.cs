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
    private TrayIcon? _trayIcon;
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
                window.DataContext = new MainViewModel(store, organizer, new DesktopDialogs(window), log);
                desktop.MainWindow = window;
                ConfigureTray(window);
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
            desktop.Exit += (_, _) => { _trayIcon?.Dispose(); _instanceLock?.Dispose(); };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureTray(Window window)
    {
        var show = new NativeMenuItem("Open Dowlume");
        show.Click += (_, _) => { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); };
        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => window.Close();
        var menu = new NativeMenu();
        menu.Items.Add(show);
        menu.Items.Add(exit);
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Dowlume — your downloads, under control",
            Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://Dowlume.App/Assets/dowlume.ico"))),
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => { window.Show(); window.Activate(); };
    }
}
