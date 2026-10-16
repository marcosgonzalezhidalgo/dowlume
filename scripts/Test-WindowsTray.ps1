function Test-WindowsTray([Diagnostics.Process]$Application) {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    if (-not ('DowlumeTrayAcceptance' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

public static class DowlumeTrayAcceptance
{
    [StructLayout(LayoutKind.Sequential)]
    private struct IconIdentifier
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public Guid Guid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { public int Left, Top, Right, Bottom; }

    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);

    public static IntPtr FindIconWindow(uint processId)
    {
        IntPtr found = IntPtr.Zero;
        WindowCallback callback = (window, parameter) =>
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != processId) return true;
            // Avalonia assigns the first tray icon ID 1 within each application process.
            var icon = new IconIdentifier { Size = (uint)Marshal.SizeOf(typeof(IconIdentifier)), Window = window, Id = 1 };
            Rectangle rectangle;
            if (Shell_NotifyIconGetRect(ref icon, out rectangle) != 0) return true;
            found = window;
            return false;
        };
        EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return found;
    }

    public static bool ClickIcon(IntPtr window) { return PostMessage(window, 0x0400 + 1024, new IntPtr(1), new IntPtr(0x0202)); }
    public static void Minimize(IntPtr window) { ShowWindow(window, 6); }

    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref IconIdentifier icon, out Rectangle rectangle);
}
'@
    }
    $Application.Refresh()
    $window = $Application.MainWindowHandle
    if ($window -eq [IntPtr]::Zero -or $Application.MainWindowTitle -ne 'Dowlume') {
        throw 'Tray acceptance requires the disposable Dowlume main window.'
    }
    $iconWindow = [DowlumeTrayAcceptance]::FindIconWindow([uint32]$Application.Id)
    if ($iconWindow -eq [IntPtr]::Zero) { throw 'Windows has no registered Dowlume notification-area icon.' }
    $automation = [Windows.Automation.AutomationElement]::FromHandle($window)
    $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, 'Hide to tray')
    $hide = $automation.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $hide) { throw 'The Hide to tray button is unavailable to Windows UI Automation.' }
    foreach ($minimized in @($false, $true)) {
        if ($minimized) { [DowlumeTrayAcceptance]::Minimize($window) }
        $hide.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
        $deadline = [DateTime]::UtcNow.AddSeconds(5)
        while ([DowlumeTrayAcceptance]::IsWindowVisible($window) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
        if ([DowlumeTrayAcceptance]::IsWindowVisible($window)) { throw 'Hide to tray did not hide the window.' }
        if (-not [DowlumeTrayAcceptance]::ClickIcon($iconWindow)) { throw 'Could not deliver the tray click.' }
        $deadline = [DateTime]::UtcNow.AddSeconds(5)
        while ((-not [DowlumeTrayAcceptance]::IsWindowVisible($window) -or [DowlumeTrayAcceptance]::IsIconic($window)) -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 50
        }
        if (-not [DowlumeTrayAcceptance]::IsWindowVisible($window) -or [DowlumeTrayAcceptance]::IsIconic($window)) {
            throw "The registered tray icon did not restore a normal window (minimized test: $minimized; visible: $([DowlumeTrayAcceptance]::IsWindowVisible($window)); minimized: $([DowlumeTrayAcceptance]::IsIconic($window)))."
        }
    }
    Write-Output 'Native notification-area registration and hidden/minimized window restoration passed.'
}
