using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Dororong.App.Interop;

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr window, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPosNative(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    internal static IntPtr GetWindowLongPtr(IntPtr window, int index)
    {
        Marshal.SetLastPInvokeError(0);
        var result = IntPtr.Size == 8
            ? GetWindowLongPtr64(window, index)
            : new IntPtr(GetWindowLong32(window, index));

        ThrowIfZeroIndicatesFailure(result);
        return result;
    }

    internal static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value)
    {
        Marshal.SetLastPInvokeError(0);
        var result = IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : new IntPtr(SetWindowLong32(window, index, value.ToInt32()));

        ThrowIfZeroIndicatesFailure(result);
        return result;
    }

    internal static void SetWindowPosChecked(
        IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags)
    {
        if (!SetWindowPosNative(window, insertAfter, x, y, width, height, flags))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private static void ThrowIfZeroIndicatesFailure(IntPtr result)
    {
        var error = Marshal.GetLastPInvokeError();
        // A zero window-long value can be valid; only a nonzero last error makes it a failure.
        if (result == IntPtr.Zero && error != 0)
        {
            throw new Win32Exception(error);
        }
    }

    internal const int GwlExStyle = -20;
    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExNoActivate = 0x08000000L;
    internal const long WsExTopmost = 0x00000008L;
    internal const int WmMouseActivate = 0x0021;
    internal const int WmLButtonDown = 0x0201;
    internal const int MaNoActivate = 3;
    internal const int VkLButton = 0x01;
    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;
    internal const uint SwpNoOwnerZOrder = 0x0200;

    internal struct POINT
    {
        internal int X;
        internal int Y;
    }
}
