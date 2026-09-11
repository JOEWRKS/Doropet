using System.ComponentModel;
using System.Runtime.InteropServices;
using Dororong.App.Interop;
using Dororong.App.Runtime;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public class NvidiaOverlayTests
{
    [Theory]
    [InlineData("CEF-OSC-WIDGET", "NVIDIA Overlay.exe", 0x800A0u, true)]
    [InlineData("CEF-OSC-WIDGET", "nvidia overlay.EXE", 0x800A0u, true)]
    [InlineData("CEF-OSC-WIDGET", "OtherApplication.exe", 0x800A0u, false)]
    [InlineData("NvidiaOrdinaryPanel", "NVIDIA Overlay.exe", 0x800A0u, false)]
    [InlineData("CEF-OSC-WIDGET", "NVIDIA Overlay.exe", 0xA0u, false)]
    [InlineData("CEF-OSC-WIDGET", "NVIDIA Overlay.exe", 0x80080u, false)]
    [InlineData("CEF-OSC-WIDGET", "NVIDIA Overlay.exe.backup", 0x800A0u, false)]
    public void Only_confirmed_transparent_layered_nvidia_widget_stops_occluding_taskbar(
        string className, string executable, uint extendedStyle, bool overlay)
    {
        using var window = new OffscreenWindow(className, extendedStyle);
        var reader = new DesktopMetadataReader(0, _ => @"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\" + executable);
        var metadata = new CapturedDesktop(reader, window.Handle);
        var native = new DesktopSceneNative(metadata);
        native.Capture(TimeSpan.Zero);
        var scene = native.Capture(TimeSpan.FromMilliseconds(80))!;
        var classified = scene.Windows[20];
        Assert.Equal((uint)Environment.ProcessId, classified.Key.ProcessId);
        Assert.Equal(window.Handle.ToInt64(), classified.Key.Handle);
        Assert.Equal(new RectD(0, 0, 1919, 1080), classified.Bounds);
        Assert.False(classified.Excluded);
        Assert.Equal(!overlay, classified.CanSupport);
        Assert.Equal(!overlay, classified.CanOcclude);
        var bar = Assert.Single(PlatformGeometry.Build(scene, 80).Where(s => s.Kind == PlatformKind.Taskbar));
        Assert.Equal(overlay ? 0 : 1919, bar.Left);
        Assert.Equal(1920, bar.Right);
        Assert.Equal(1040, bar.Top);
    }

    [Fact]
    public void Captured_thirteen_second_overlay_does_not_drop_supported_pet_from_y833()
    {
        using var window = new OffscreenWindow("CEF-OSC-WIDGET", 0x800A0);
        var reader = new DesktopMetadataReader(0, _ => @"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA Overlay.exe");
        var metadata = new CapturedDesktop(reader, window.Handle) { OverlayPresent = false };
        var source = new DesktopSceneSource(new DesktopSceneNative(metadata));
        source.Read(TimeSpan.Zero);
        var motion = new PlatformMotion();
        var foot = new FootContact(63, 90, 207, 127);
        var position = new PointD(400, 833);
        for (var milliseconds = 80; milliseconds <= 13760; milliseconds += 80)
        {
            // The captured widget spans x0..1919 across the bar top for about13s.
            metadata.OverlayPresent = milliseconds >= 160 && milliseconds < 13280;
            var read = source.Read(TimeSpan.FromMilliseconds(milliseconds));
            Assert.Equal(SceneReadHealth.Fresh, read.Health);
            var pose = motion.Advance(new(TimeSpan.FromMilliseconds(80), position, position, foot,
                PlatformGeometry.Build(read.Scene!, 80), false, true));
            Assert.Equal(PlatformPhase.Supported, pose.Phase);
            Assert.Equal(833, pose.Position.Y);
            Assert.Equal(1040, pose.Position.Y + foot.SoleY);
            Assert.Equal(65786, pose.Support!.Value.Handle);
            position = pose.Position;
        }
    }

    [Fact]
    public void Identity_denied_widget_remains_ordinary_with_fresh_geometry()
    {
        using var window = new OffscreenWindow("CEF-OSC-WIDGET", 0x800A0);
        var reader = new DesktopMetadataReader(0, _ => throw new Win32Exception(5));
        var source = new DesktopSceneSource(new DesktopSceneNative(new CapturedDesktop(reader, window.Handle)));
        var read = source.Read(TimeSpan.Zero);
        Assert.Equal(SceneReadHealth.Fresh, read.Health);
        Assert.Null(source.LastFailure);
        Assert.True(read.Scene!.Windows[20].CanSupport);
        Assert.True(read.Scene.Windows[20].CanOcclude);
        Assert.Equal(new RectD(0, 0, 1919, 1080), read.Scene.Windows[20].Bounds);
    }

    private sealed class CapturedDesktop(DesktopMetadataReader reader, nint handle) : IDesktopMetadataReader
    {
        internal bool OverlayPresent = true;
        public NativeDesktop Read()
        {
            var bar = new NativeWindow(65786, 8556, new(0, 1040, 1920, 40),
                Taskbar: true, HorizontalTaskbar: true, ClassName: "Shell_TrayWnd");
            var windows = new List<NativeWindow>();
            // Preserve captured relative z-order: widget20, taskbar27. Empty
            // placeholders carry no geometry and do not affect occlusion.
            for (var z = 0; z < 27; z++)
            {
                if (z == 20 && OverlayPresent)
                {
                    var widget = reader.ReadWindow(handle)!;
                    // Read real native PID/class/styles/DWM bounds offscreen;
                    // translate only position into the hand-recorded desktop fixture.
                    Assert.Equal(new RectD(-30000, -30000, 1919, 1080), widget.Bounds);
                    windows.Add(widget with { Bounds = new(0, 0, 1919, 1080) });
                }
                else windows.Add(new(1000 + z, 1, new(), Visible: false));
            }
            windows.Add(bar);
            return new([new(1, new(0, 0, 1920, 1080))], windows);
        }
    }

    private sealed class OffscreenWindow : IDisposable
    {
        private readonly string className;
        private static readonly WindowProcedure Procedure = DefWindowProc;
        private readonly nint instance = GetModuleHandle(null);
        internal nint Handle { get; }

        internal OffscreenWindow(string className, uint extendedStyle)
        {
            this.className = className;
            var definition = new WindowClass
            {
                Procedure = Marshal.GetFunctionPointerForDelegate(Procedure),
                Instance = instance, ClassName = className
            };
            Assert.NotEqual(0, RegisterClass(ref definition));
            Handle = CreateWindowEx(extendedStyle | 0x08000000, className, "", 0x90000000,
                -30000, -30000, 1919, 1080, 0, 0, instance, 0);
            Assert.NotEqual(0, Handle);
        }

        public void Dispose()
        {
            DestroyWindow(Handle);
            UnregisterClass(className, instance);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClass
        {
            internal uint Style;
            internal nint Procedure;
            internal int ClassExtra, WindowExtra;
            internal nint Instance, Icon, Cursor, Background;
            internal string? MenuName;
            internal string ClassName;
        }
        private delegate nint WindowProcedure(nint window, uint message, nint wParam, nint lParam);
        [DllImport("user32.dll", EntryPoint = "RegisterClassW", CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClass(ref WindowClass definition);
        [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterClass(string className, nint instance);
        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
        private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);
        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
        private static extern nint GetModuleHandle(string? module);
        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
        private static extern nint CreateWindowEx(uint extendedStyle, string className, string name, uint style,
            int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(nint handle);
    }
}
