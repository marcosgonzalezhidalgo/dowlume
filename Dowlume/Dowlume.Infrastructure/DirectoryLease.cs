using Microsoft.Win32.SafeHandles;

namespace Dowlume.Infrastructure;

internal sealed class DirectoryLease : IDisposable
{
    private readonly List<SafeFileHandle> _handles = [];

    internal static DirectoryLease Acquire(params string[] filePaths)
    {
        var lease = new DirectoryLease();
        try
        {
            foreach (string path in filePaths)
            {
                if (OperatingSystem.IsWindows())
                {
                    var ancestors = new Stack<string>();
                    for (string? parent = Path.GetDirectoryName(Path.GetFullPath(path)); parent is not null; parent = Path.GetDirectoryName(parent))
                    {
                        ancestors.Push(parent);
                    }
                    foreach (string directory in ancestors)
                    {
                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                        lease._handles.Add(WindowsFileHandle.LockDirectory(directory));
                        PathSafety.RejectLinks(directory);
                    }
                }
                PathSafety.RejectLinks(path);
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        for (int index = _handles.Count - 1; index >= 0; index--)
        {
            _handles[index].Dispose();
        }
        _handles.Clear();
    }
}
