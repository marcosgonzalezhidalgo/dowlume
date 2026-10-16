using System.Runtime.InteropServices;

namespace Dowlume.Infrastructure;

public static class DownloadsFolder
{
    public static string Suggest()
    {
        if (OperatingSystem.IsWindows())
        {
            Guid downloadsId = new("374DE290-123F-4565-9164-39C4925E467B");
            int result = SHGetKnownFolderPath(ref downloadsId, 0, IntPtr.Zero, out IntPtr path);
            try
            {
                if (result == 0)
                {
                    return Marshal.PtrToStringUni(path)!;
                }
            }
            finally { Marshal.FreeCoTaskMem(path); }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
