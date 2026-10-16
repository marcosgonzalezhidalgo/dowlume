using Dowlume.App.Services;
using Microsoft.Win32;

namespace Dowlume.Tests;

public sealed class WindowsStartupTests
{
    [Fact]
    public void StartupQuotesTheExecutableAndOnlyRemovesItsOwnRegistration()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string executable = workspace.CreateFile("Application with spaces/Dowlume.App.exe");
        string registryPath = @"Software\Dowlume.Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            var startup = new WindowsStartupRegistration(executable, registryPath);
            Assert.False(startup.IsEnabled);
            startup.SetEnabled(true);
            Assert.True(startup.IsEnabled);
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true)!;
            Assert.Equal($"\"{executable}\"", key.GetValue("Dowlume"));
            key.SetValue("Unrelated", "Retain this value");
            startup.SetEnabled(false);
            Assert.Null(key.GetValue("Dowlume"));
            Assert.Equal("Retain this value", key.GetValue("Unrelated"));
            key.SetValue("Dowlume", "A different portable installation");
            startup.SetEnabled(false);
            Assert.Equal("A different portable installation", key.GetValue("Dowlume"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void DevelopmentHostCannotRegisterItselfForStartup()
    {
        var startup = new WindowsStartupRegistration(Environment.ProcessPath, @"Software\Dowlume.Tests\Unused");
        Assert.False(startup.IsSupported);
        Assert.Throws<PlatformNotSupportedException>(() => startup.SetEnabled(true));
    }
}
