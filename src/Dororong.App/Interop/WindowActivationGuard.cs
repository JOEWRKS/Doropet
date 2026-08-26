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

    internal static bool TryHandleMessage(int message, out IntPtr result)
    {
        if (message == NativeMethods.WmMouseActivate)
        {
            // MA_NOACTIVATE prevents activation without discarding the following mouse message.
            result = new IntPtr(NativeMethods.MaNoActivate);
            return true;
        }

        result = IntPtr.Zero;
        return false;
    }

    private static IntPtr WindowProcedure(
        IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = TryHandleMessage(message, out var result);
        return result;
    }

    private void OnSourceDisposed(object? sender, EventArgs e)
    {
        IsAttached = false;
        _source = null;
        _hook = null;
    }
}
