using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace Dowlume.App.Services;

public sealed class DesktopDialogs(Window owner) : IDesktopDialogs
{
    public async Task<string?> ChooseFolderAsync(string title)
    {
        IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public Task<bool> ConfirmAsync(string title, string message, string action)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var cancel = new Button { Content = "Cancel" };
        var confirm = new Button { Content = action };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 24,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 12, Children = { cancel, confirm } }
            }
        };
        return dialog.ShowDialog<bool>(owner);
    }

    public void RevealFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Explorer is available on Windows.");
        }
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add("/select,");
        start.ArgumentList.Add(Path.GetFullPath(path));
        Process.Start(start)?.Dispose();
    }
}
