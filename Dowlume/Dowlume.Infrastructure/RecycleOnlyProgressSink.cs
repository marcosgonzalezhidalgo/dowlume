using System.Runtime.InteropServices;

namespace Dowlume.Infrastructure;

// Windows must explicitly mark each item as recyclable before deletion is permitted.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class RecycleOnlyProgressSink : IFileOperationProgressSink
{
    public bool Recycled
    {
        get; private set;
    }
    public int Result
    {
        get; private set;
    }

    public int PreDeleteItem(uint flags, IntPtr item) => (flags & 0x80) != 0 ? 0 : unchecked((int)0x80004004);

    public int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr destination)
    {
        Result = result;
        Recycled = result >= 0 && destination != IntPtr.Zero;
        return 0;
    }

    public int StartOperations() => 0;
    public int FinishOperations(int result) => 0;
    public int PreRenameItem(uint flags, IntPtr item, string name) => 0;
    public int PostRenameItem(uint flags, IntPtr item, string name, int result, IntPtr destination) => 0;
    public int PreMoveItem(uint flags, IntPtr item, IntPtr folder, string name) => 0;
    public int PostMoveItem(uint flags, IntPtr item, IntPtr folder, string name, int result, IntPtr destination) => 0;
    public int PreCopyItem(uint flags, IntPtr item, IntPtr folder, string name) => 0;
    public int PostCopyItem(uint flags, IntPtr item, IntPtr folder, string name, int result, IntPtr destination) => 0;
    public int PreNewItem(uint flags, IntPtr folder, string name) => 0;
    public int PostNewItem(uint flags, IntPtr folder, string name, string template, uint attributes, int result, IntPtr destination) => 0;
    public int UpdateProgress(uint total, uint completed) => 0;
    public int ResetTimer() => 0;
    public int PauseTimer() => 0;
    public int ResumeTimer() => 0;
}

[ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IFileOperationProgressSink
{
    [PreserveSig] int StartOperations();
    [PreserveSig] int FinishOperations(int result);
    [PreserveSig] int PreRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostRenameItem(uint flags, IntPtr item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr destination);
    [PreserveSig] int PreMoveItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostMoveItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr destination);
    [PreserveSig] int PreCopyItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostCopyItem(uint flags, IntPtr item, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IntPtr destination);
    [PreserveSig] int PreDeleteItem(uint flags, IntPtr item);
    [PreserveSig] int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr destination);
    [PreserveSig] int PreNewItem(uint flags, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostNewItem(uint flags, IntPtr folder, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string template, uint attributes, int result, IntPtr destination);
    [PreserveSig] int UpdateProgress(uint total, uint completed);
    [PreserveSig] int ResetTimer();
    [PreserveSig] int PauseTimer();
    [PreserveSig] int ResumeTimer();
}
