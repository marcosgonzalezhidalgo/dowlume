using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Dowlume.App.ViewModels;

namespace Dowlume.App.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _closing;
    private bool _canClose;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _refreshTimer.Tick += async (_, _) =>
        {
            if (DataContext is MainViewModel model)
            {
                await model.RefreshAsync(skipIfRefreshing: true);
            }
        };
        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel model)
            {
                await model.RefreshAsync();
            }
            _refreshTimer.Start();
        };
        Closing += OnClosing;
    }

    private void OnHideToTray(object? sender, RoutedEventArgs args) => Hide();

    private async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_canClose)
        {
            return;
        }
        args.Cancel = true;
        if (_closing)
        {
            return;
        }
        _closing = true;
        // Closing a pending confirmation returns its default false result before shutdown waits for it.
        foreach (Window dialog in OwnedWindows.ToArray())
        {
            dialog.Close();
        }
        IsEnabled = false;
        _refreshTimer.Stop();
        if (DataContext is MainViewModel model)
        {
            await model.ShutdownAsync();
        }
        _canClose = true;
        Close();
    }
}
