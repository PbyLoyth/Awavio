using System.Runtime.InteropServices;

namespace Awavio.Windows;

static partial class NativeMethods
{
    [Flags]
    public enum ExecutionState : uint
    {
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
        Continuous = 0x80000000,
    }

    [LibraryImport("kernel32.dll")]
    public static partial ExecutionState SetThreadExecutionState(ExecutionState flags);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerDuplicateScheme(IntPtr userRootPowerKey, in Guid source, out IntPtr destination);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerDeleteScheme(IntPtr userRootPowerKey, in Guid scheme);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerSetActiveScheme(IntPtr userRootPowerKey, in Guid schemeGuid);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerReadACValueIndex(IntPtr rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, out uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerReadDCValueIndex(IntPtr rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, out uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerWriteACValueIndex(IntPtr rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerWriteDCValueIndex(IntPtr rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, uint value);

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr LocalFree(IntPtr mem);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr icon);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>Moves a window to the top of the topmost band without activating it.</summary>
    public static void BringToTopNoActivate(IntPtr hWnd)
    {
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
        SetWindowPos(hWnd, new IntPtr(-1) /* HWND_TOPMOST */, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }
}
