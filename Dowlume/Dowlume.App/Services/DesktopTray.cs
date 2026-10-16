using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;

namespace Dowlume.App.Services;

public sealed class DesktopTray : IDisposable
{
    private readonly Application _application;
    private readonly TrayIcon _icon;

    public DesktopTray(Application application, Window window)
    {
        _application = application;
        var open = new RelayCommand(() =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        });
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("Open Dowlume") { Command = open });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Exit") { Command = new RelayCommand(window.Close) });
        using Stream iconStream = AssetLoader.Open(new Uri("avares://Dowlume.App/Assets/dowlume.ico"));
        _icon = new TrayIcon
        {
            ToolTipText = "Dowlume — your downloads, under control",
            Icon = new WindowIcon(iconStream),
            Menu = menu,
            Command = open,
            IsVisible = true
        };
        // Avalonia creates the native icon only when it belongs to this collection.
        TrayIcon.SetIcons(application, new TrayIcons { _icon });
    }

    public void Dispose()
    {
        TrayIcon.SetIcons(_application, null);
        _icon.Dispose();
    }
}
