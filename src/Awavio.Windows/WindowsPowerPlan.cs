using System.ComponentModel;
using System.Runtime.InteropServices;
using Awavio.Core;

namespace Awavio.Windows;

/// <summary>The lid action of a power plan, through powrprof.dll. This is the
/// same setting as Control Panel > Power Options > "Choose what closing the lid does".</summary>
sealed class WindowsPowerPlan : IPowerPlan
{
    // GUID_SYSTEM_BUTTON_SUBGROUP and GUID_LIDCLOSE_ACTION from winnt.h.
    static readonly Guid ButtonsSubgroup = new("4f971e89-eebd-4455-a8de-9e59040e7347");
    static readonly Guid LidCloseAction = new("5ca83367-6e45-459f-a27b-476b1d01c936");

    public Guid ActiveScheme()
    {
        Check(NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var ptr), "PowerGetActiveScheme");
        try
        {
            return Marshal.PtrToStructure<Guid>(ptr);
        }
        finally
        {
            NativeMethods.LocalFree(ptr);
        }
    }

    public LidActions ReadLid(Guid scheme)
    {
        Check(NativeMethods.PowerReadACValueIndex(IntPtr.Zero, scheme, ButtonsSubgroup, LidCloseAction, out var ac), "PowerReadACValueIndex");
        Check(NativeMethods.PowerReadDCValueIndex(IntPtr.Zero, scheme, ButtonsSubgroup, LidCloseAction, out var dc), "PowerReadDCValueIndex");
        return new LidActions(ac, dc);
    }

    public void WriteLid(Guid scheme, LidActions actions)
    {
        Check(NativeMethods.PowerWriteACValueIndex(IntPtr.Zero, scheme, ButtonsSubgroup, LidCloseAction, actions.Ac), "PowerWriteACValueIndex");
        Check(NativeMethods.PowerWriteDCValueIndex(IntPtr.Zero, scheme, ButtonsSubgroup, LidCloseAction, actions.Dc), "PowerWriteDCValueIndex");
        // Written values only take effect once the plan is (re)applied.
        if (ActiveScheme() == scheme)
            Check(NativeMethods.PowerSetActiveScheme(IntPtr.Zero, scheme), "PowerSetActiveScheme");
    }

    static void Check(uint error, string call)
    {
        if (error != 0) throw new Win32Exception((int)error, $"{call} failed: {new Win32Exception((int)error).Message}");
    }
}

/// <summary>SetThreadExecutionState. Windows drops the request when the calling
/// thread exits, so a crash can never leave the PC stuck awake. Call from the UI thread.</summary>
sealed class ExecutionStateKeepAwake : IKeepAwake
{
    public void Hold(bool keepDisplayOn)
    {
        var flags = NativeMethods.ExecutionState.Continuous | NativeMethods.ExecutionState.SystemRequired;
        if (keepDisplayOn) flags |= NativeMethods.ExecutionState.DisplayRequired;
        if (NativeMethods.SetThreadExecutionState(flags) == 0)
            throw new Win32Exception("SetThreadExecutionState failed.");
    }

    public void Release() => NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.Continuous);
}
