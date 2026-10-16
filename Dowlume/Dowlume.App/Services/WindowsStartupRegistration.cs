using Microsoft.Win32;

namespace Dowlume.App.Services;

public sealed class WindowsStartupRegistration : IStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string? _executablePath;
    private readonly string _registryPath;

    public WindowsStartupRegistration() : this(Environment.ProcessPath, RunKey) { }

    internal WindowsStartupRegistration(string? executablePath, string registryPath)
    {
        _executablePath = executablePath;
        _registryPath = registryPath;
    }

    public bool IsSupported => OperatingSystem.IsWindows() &&
        string.Equals(Path.GetFileName(_executablePath), "Dowlume.App.exe", StringComparison.OrdinalIgnoreCase);

    private string Command => $"\"{_executablePath}\"";

    public bool IsEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows() || !IsSupported)
            {
                return false;
            }
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_registryPath);
            return string.Equals(key?.GetValue("Dowlume") as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows() || !IsSupported)
        {
            throw new PlatformNotSupportedException("Windows startup requires the packaged Dowlume application.");
        }
        if (enabled)
        {
            if (!Path.IsPathFullyQualified(_executablePath!) || !File.Exists(_executablePath) || Command.Length > 260)
            {
                throw new IOException("The application path is unavailable or too long for Windows startup.");
            }
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(_registryPath);
            key.SetValue("Dowlume", Command, RegistryValueKind.String);
        }
        else
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_registryPath, writable: true);
            if (string.Equals(key?.GetValue("Dowlume") as string, Command, StringComparison.OrdinalIgnoreCase))
            {
                key!.DeleteValue("Dowlume", throwOnMissingValue: false);
            }
        }
    }
}
