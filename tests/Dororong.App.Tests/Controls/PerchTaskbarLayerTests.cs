using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Dororong.App.Controls;
using Dororong.App.Interop;
using Dororong.App.Runtime;

namespace Dororong.App.Tests.Controls;

public sealed class PerchTaskbarLayerTests
{
    // Catches missing production wiring as well as a repair that changes focus,
    // geometry, styles, or repeatedly raises an already correctly ordered pet.
    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void Perched_normal_host_recovers_above_overlapping_taskbar_without_activation_or_churn(string className) => CheekProductTests.Sta(() =>
    {
        using var h = new NativePair(className, overlap: true);
        Assert.True(Above(h.Support, h.Pet));
        var petBounds = Bounds(h.Pet); var supportBounds = Bounds(h.Support);
        var petStyle = NativeMethods.GetWindowLongPtr(h.Pet, -20);
        var supportStyle = NativeMethods.GetWindowLongPtr(h.Support, -20);
        var foreground = Api.GetForegroundWindow();

        h.Maintain();

        Assert.True(Above(h.Pet, h.Support), "Perched pet must be above the overlapping topmost taskbar.");
        Assert.Equal(petBounds, Bounds(h.Pet)); Assert.Equal(supportBounds, Bounds(h.Support));
        Assert.Equal(petStyle, NativeMethods.GetWindowLongPtr(h.Pet, -20));
        Assert.Equal(supportStyle, NativeMethods.GetWindowLongPtr(h.Support, -20));
        Assert.Equal(foreground, Api.GetForegroundWindow());
        Assert.Equal(1, h.PetPositionMessages);
        h.Maintain(); h.Maintain();
        Assert.Equal(1, h.PetPositionMessages);

        // A later shell raise is repaired again; this is not only an entry fix.
        NativeMethods.SetWindowPosChecked(h.Support, new(-1), 0, 0, 0, 0, 0x13);
        Assert.True(Above(h.Support, h.Pet));
        h.Maintain();
        Assert.True(Above(h.Pet, h.Support));
        Assert.Equal(foreground, Api.GetForegroundWindow());
    });

    [Theory]
    [InlineData("Shell_TrayWnd", false)]
    [InlineData("DororongTestOrdinaryTopmost", true)]
    public void Nonoverlapping_taskbar_and_ordinary_topmost_window_do_not_raise_pet(string className, bool overlap) => CheekProductTests.Sta(() =>
    {
        using var h = new NativePair(className, overlap);
        Assert.True(Above(h.Support, h.Pet));
        h.Maintain();
        Assert.True(Above(h.Support, h.Pet));
        Assert.Equal(0, h.PetPositionMessages);
    });

    private static (int Left, int Top, int Right, int Bottom) Bounds(nint handle)
    {
        Assert.True(DesktopMetadataReader.Api.GetWindowRect(handle, out var r));
        return (r.Left, r.Top, r.Right, r.Bottom);
    }

    private static bool Above(nint first, nint second)
    {
        for (var h = DesktopMetadataReader.Api.GetWindow(first, 2); h != 0; h = DesktopMetadataReader.Api.GetWindow(h, 2))
            if (h == second) return true;
        return false;
    }

    private sealed class NativePair : IDisposable
    {
        private readonly string _className;
        private readonly Api.WndProc _procedure = Api.DefWindowProcW;
        private readonly Window _window;
        private readonly WindowActivationGuard _activation;
        private readonly HwndSource _source;
        private readonly PetLoopHost _host;
        internal nint Pet { get; }
        internal nint Support { get; }
        internal int PetPositionMessages { get; private set; }

        internal NativePair(string className, bool overlap)
        {
            _className = className;
            var wc = new Api.WndClass { Procedure = _procedure, Instance = Api.GetModuleHandleW(null), ClassName = className };
            Assert.NotEqual((ushort)0, Api.RegisterClassW(ref wc));
            var presenter = new DororongPresenter(); var root = new Grid(); root.Children.Add(presenter);
            _window = new Window { Width = 144, Height = 144, Left = -30000, Top = -30000, Content = root,
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Opacity = 0,
                ShowActivated = false, ShowInTaskbar = false, Topmost = true };
            _window.SourceInitialized += (_, _) => WindowStyleManager.ApplyNoActivateToolWindow(new WindowInteropHelper(_window).Handle);
            _window.Show();
            Pet = new WindowInteropHelper(_window).Handle;
            _source = HwndSource.FromHwnd(Pet)!;
            _activation = new WindowActivationGuard(_source);
            var factory = typeof(PetLoop).GetMethod("CreateProductionRuntime", BindingFlags.NonPublic | BindingFlags.Static)!;
            var runtime = factory.Invoke(null, [_window, presenter, new DesktopInput()])!;
            _host = (PetLoopHost)runtime.GetType().GetProperty("Host")!.GetValue(runtime)!;
            _window.UpdateLayout();
            var bounds = Bounds(Pet);
            Support = Api.CreateWindowExW(0x08080088, className, "", 0x80000000,
                overlap ? bounds.Left : bounds.Right + 50, bounds.Top + 190, 336, 40,
                0, 0, wc.Instance, 0);
            Assert.NotEqual(0, Support);
            Assert.True(Api.SetLayeredWindowAttributes(Support, 0, 0, 2));
            Api.ShowWindow(Support, 4);
            NativeMethods.SetWindowPosChecked(Support, new(-1), 0, 0, 0, 0, 0x13);
            _source.AddHook(ObservePosition);
        }

        internal void Maintain() => _host.MaintainPerchLayer?.Invoke();

        private nint ObservePosition(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        {
            if (message == 0x0046) PetPositionMessages++;
            return 0;
        }

        public void Dispose()
        {
            _source.RemoveHook(ObservePosition);
            _activation.Dispose();
            Api.DestroyWindow(Support);
            _window.Close();
            Api.UnregisterClassW(_className, Api.GetModuleHandleW(null));
            GC.KeepAlive(_procedure);
        }
    }

    private static class Api
    {
        internal delegate nint WndProc(nint window, uint message, nint wParam, nint lParam);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WndClass
        {
            internal uint Style; internal WndProc Procedure; internal int ClassExtra, WindowExtra;
            internal nint Instance, Icon, Cursor, Background; internal string? MenuName; internal string ClassName;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern ushort RegisterClassW(ref WndClass windowClass);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool UnregisterClassW(string name, nint instance);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint data);
        [DllImport("user32.dll")] internal static extern nint DefWindowProcW(nint window, uint message, nint wParam, nint lParam);
        [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint window);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(nint window, int command);
        [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(nint window, uint color, byte alpha, uint flags);
        [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandleW(string? module);
    }
}
