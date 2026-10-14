using Avalonia;

namespace Dowlume.App;

internal static class Program
{
    internal static string DataDirectory { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dowlume");

    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--data-directory", var directory])
        {
            DataDirectory = Path.GetFullPath(directory);
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .WithInterFont()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
    }
}
