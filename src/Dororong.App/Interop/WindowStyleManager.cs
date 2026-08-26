namespace Dororong.App.Interop;

internal static class WindowStyleManager
{
    internal static void ApplyNoActivateToolWindow(IntPtr window)
    {
        var currentStyle = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle).ToInt64();
        var updatedStyle = currentStyle | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;

        NativeMethods.SetWindowLongPtr(window, NativeMethods.GwlExStyle, new IntPtr(updatedStyle));
        NativeMethods.SetWindowPos(
            window,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoSize |
            NativeMethods.SwpNoMove |
            NativeMethods.SwpNoZOrder |
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpFrameChanged);
    }
}
