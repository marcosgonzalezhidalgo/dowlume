using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Dowlume.Core;

namespace Dowlume.Infrastructure;

public sealed class WindowsRecycleBin : IRecycleBin
{
    public Task RecycleAsync(string path, string expectedHash)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
        {
            throw new PlatformNotSupportedException("Recycling requires Windows 8 or later.");
        }
        PathSafety.RejectLinks(path);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                {
                    throw new PlatformNotSupportedException();
                }
                RecycleOnStaThread(path, expectedHash);
                completion.SetResult();
            }
            catch (Exception error)
            {
                completion.SetException(error);
            }
        })
        {
            IsBackground = true,
            Name = "Dowlume recycle operation"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    [SupportedOSPlatform("windows6.2")]
    private static void RecycleOnStaThread(string path, string expectedHash)
    {
        using var directories = DirectoryLease.Acquire(path);
        var mover = new SafeFileMover();
        using FileStream verifiedSource = mover.OpenVerifiedRead(path, expectedHash);
        var operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"), throwOnError: true)!)!;
        IShellItem? item = null;
        try
        {
            Guid shellItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItemId, out item));
            const uint recycleOnly = 0x00080000;
            const uint failEarly = 0x00100000;
            const uint noErrorUi = 0x0400;
            const uint silent = 0x0004;
            const uint noConfirmation = 0x0010;
            operation.SetOperationFlags(recycleOnly | failEarly | noErrorUi | silent | noConfirmation);
            var sink = new RecycleOnlyProgressSink(() =>
            {
                // The shell resolves paths asynchronously; reject replacement before it acts.
                using FileStream currentSource = mover.OpenVerifiedRead(path, expectedHash);
            });
            operation.DeleteItem(item, sink);
            operation.PerformOperations();
            operation.GetAnyOperationsAborted(out bool aborted);
            Marshal.ThrowExceptionForHR(sink.Result);
            if (aborted || !sink.Recycled || File.Exists(path))
            {
                throw new IOException($"Windows could not recycle this file. No permanent-delete fallback is available. Shell result: {sink.Result:X8}.");
            }
        }
        finally
        {
            if (item is not null)
            {
                Marshal.ReleaseComObject(item);
            }
            Marshal.ReleaseComObject(operation);
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid interfaceId, out IShellItem item);

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handlerId, ref Guid interfaceId, out IntPtr handler);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint displayName, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    // Keep the complete native vtable order, including methods not used by this adapter.
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(IntPtr sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr dialog);
        void SetProperties(IntPtr properties);
        void SetOwnerWindow(uint window);
        void ApplyPropertiesToItem(IntPtr item);
        void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        void MoveItem(IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        void MoveItems(IntPtr items, IntPtr folder);
        void CopyItem(IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        void CopyItems(IntPtr items, IntPtr folder);
        void DeleteItem(IShellItem item, IFileOperationProgressSink sink);
        void DeleteItems(IntPtr items);
        void NewItem(IntPtr folder, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, IntPtr sink);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
