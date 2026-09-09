using System.Windows.Interop;

namespace Dororong.App.Interop;

internal sealed class WindowActivationGuard : IDisposable
{
    private HwndSource? _source;
    private HwndSourceHook? _hook;

    internal WindowActivationGuard(HwndSource source)
    {
        _source = source;
        _hook = WindowProcedure;

        source.AddHook(_hook);
        source.Disposed += OnSourceDisposed;
        IsAttached = true;
    }

    internal bool IsAttached { get; private set; }

    public void Dispose()
    {
        var source = _source;
        var hook = _hook;
        if (source is null || hook is null)
        {
            return;
        }

        source.Disposed -= OnSourceDisposed;
        if (IsAttached && !source.IsDisposed)
        {
            source.RemoveHook(hook);
        }

        IsAttached = false;
        _source = null;
        _hook = null;
    }

    internal static bool TryHandleMessage(int message, out IntPtr result,
        bool primaryButtonDown = true, Action? activateForPress = null)
    {
        // UIPI can return zero for a real held press when an elevated window is
        // foreground. Explicit activation is necessary for WS_EX_NOACTIVATE;
        // MA_ACTIVATE alone does not restore polling on that window style.
        // Request it only on a delivered left-down, before WPF queues the press.
        if (message == NativeMethods.WmLButtonDown && !primaryButtonDown)
            activateForPress?.Invoke();

        if (message == NativeMethods.WmMouseActivate)
        {
            result = new IntPtr(NativeMethods.MaNoActivate);
            return true;
        }

        result = IntPtr.Zero;
        return false;
    }

    private static IntPtr WindowProcedure(
        IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var buttonDown = message != NativeMethods.WmLButtonDown ||
            (NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) != 0;
        handled = TryHandleMessage(message, out var result, buttonDown,
            () => NativeMethods.SetForegroundWindow(window));
        return result;
    }

    private void OnSourceDisposed(object? sender, EventArgs e)
    {
        IsAttached = false;
        _source = null;
        _hook = null;
    }
}
