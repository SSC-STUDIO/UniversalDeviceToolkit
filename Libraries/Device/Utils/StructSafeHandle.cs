using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace UniversalDeviceToolkit.Lib.Utils;

public sealed class StructSafeHandle<T> : SafeHandle where T : struct
{
    private IntPtr _ptr;

    public StructSafeHandle(T str) : base(IntPtr.Zero, true)
    {
        var size = Marshal.SizeOf(typeof(T));
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(str, ptr, false);
        SetHandle(ptr);
        _ptr = ptr;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        var ptr = Interlocked.Exchange(ref _ptr, IntPtr.Zero);
        if (ptr != IntPtr.Zero)
            Marshal.FreeHGlobal(ptr);

        handle = IntPtr.Zero;
        return true;
    }
}
