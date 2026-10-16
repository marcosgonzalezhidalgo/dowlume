using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Dowlume.Infrastructure;

[SupportedOSPlatform("windows")]
internal static class WindowsFileHandle
{
    private const uint OpenExisting = 3;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint BackupSemantics = 0x02000000;
    private const uint GenericRead = 0x80000000;
    private const uint DeleteAccess = 0x00010000;
    private const int NotSameDevice = 17;

    internal static FileStream OpenForMove(string path)
    {
        SafeFileHandle handle = Open(path, GenericRead | DeleteAccess, FileShare.Read, OpenReparsePoint);
        try
        {
            return new FileStream(handle, FileAccess.Read);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static SafeFileHandle LockDirectory(string path) =>
        Open(path, GenericRead, FileShare.Read | FileShare.Write, BackupSemantics | OpenReparsePoint);

    private static SafeFileHandle Open(string path, uint access, FileShare share, uint flags)
    {
        SafeFileHandle handle = CreateFileW(path, access, share, IntPtr.Zero, OpenExisting, flags, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw FileError(error);
        }
        return handle;
    }

    internal static bool TryRename(SafeFileHandle source, string destination)
    {
        byte[] name = Encoding.Unicode.GetBytes(Path.GetFullPath(destination));
        int nameOffset = Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.FirstCharacter)).ToInt32();
        int size = checked(nameOffset + name.Length + sizeof(char));
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(new byte[size], 0, buffer, size);
            // ReplaceIfExists remains false; never replace an existing destination.
            Marshal.WriteInt32(buffer, Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.NameLength)).ToInt32(), name.Length);
            Marshal.Copy(name, 0, IntPtr.Add(buffer, nameOffset), name.Length);
            if (SetFileInformationByHandle(source, 3, buffer, (uint)size))
            {
                return true;
            }
            int error = Marshal.GetLastPInvokeError();
            if (error == NotSameDevice)
            {
                return false;
            }
            throw FileError(error);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static void CopyWithoutReplacing(string source, string destination)
    {
        // Windows copies alternate streams and file attributes as well as the main stream.
        if (!CopyFileW(source, destination, failIfExists: true))
        {
            throw FileError(Marshal.GetLastPInvokeError());
        }
    }

    internal static void DeleteVerifiedSource(SafeFileHandle source)
    {
        IntPtr disposition = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(disposition, 1);
            if (!SetFileInformationByHandle(source, 4, disposition, 1))
            {
                throw FileError(Marshal.GetLastPInvokeError());
            }
        }
        finally { Marshal.FreeHGlobal(disposition); }
    }

    private static IOException FileError(int error) =>
        new("Windows could not safely complete the file operation.", new Win32Exception(error));

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RenameInformation
    {
        public uint ReplaceIfExists;
        public IntPtr RootDirectory;
        public uint NameLength;
        public char FirstCharacter;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, FileShare share,
        IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr template);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, IntPtr information, uint size);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CopyFileW(string source, string destination, [MarshalAs(UnmanagedType.Bool)] bool failIfExists);
}
