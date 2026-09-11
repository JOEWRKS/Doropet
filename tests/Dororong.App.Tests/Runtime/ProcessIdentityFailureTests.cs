using System.ComponentModel;
using System.Runtime.InteropServices;
using Dororong.App.Interop;
using Dororong.App.Runtime;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public class ProcessIdentityFailureTests
{
    [Fact]
    public void Existing_metadata_probe_can_construct_reader_using_first_reflected_constructor()
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance;
        // Verify-WindowPlatforms.ps1 consumes this exact legacy entry point.
        var reader = (DesktopMetadataReader)typeof(DesktopMetadataReader).GetConstructors(flags)[0]
            .Invoke([nint.Zero]);
        Assert.Null(reader.ReadWindow(0));
    }

    [Fact]
    public void Denied_optional_identity_keeps_fresh_geometry_and_taskbar_support_beyond_scene_expiry()
    {
        using var window = new OffscreenWindow();
        var denied = false;
        var reader = new DesktopMetadataReader(0, _ => denied
            ? throw new Win32Exception(5) : @"C:\Games\Game.exe");
        var source = new DesktopSceneSource(new DesktopSceneNative(new WindowMetadata(reader, window.Handle)));
        source.Read(TimeSpan.Zero);
        var initial = source.Read(TimeSpan.FromMilliseconds(80));
        var key = initial.Scene!.Windows[0].Key;
        var motion = new PlatformMotion();
        var foot = new FootContact(20, 45, 111, 48);
        var position = new PointD(400, 929);
        var initialPose = motion.Advance(new(TimeSpan.FromMilliseconds(16), position, position, foot,
            PlatformGeometry.Build(initial.Scene, 63), false, true));
        Assert.Equal(PlatformPhase.Supported, initialPose.Phase);

        denied = true;
        for (var milliseconds = 160; milliseconds <= 1200; milliseconds += 80)
        {
            var read = source.Read(TimeSpan.FromMilliseconds(milliseconds));
            Assert.Equal(SceneReadHealth.Fresh, read.Health);
            Assert.Null(source.LastFailure);
            var ordinary = read.Scene!.Windows[0];
            Assert.Equal(key, ordinary.Key);
            Assert.Equal((uint)Environment.ProcessId, ordinary.Key.ProcessId);
            Assert.Equal(new RectD(-30000, -30000, 320, 160), ordinary.Bounds);
            Assert.False(ordinary.Excluded);
            Assert.True(ordinary.CanSupport);
            Assert.True(ordinary.CanOcclude);
            var pose = motion.Advance(new(TimeSpan.FromMilliseconds(80), position, position, foot,
                PlatformGeometry.Build(read.Scene, 63), false, true));
            Assert.Equal(PlatformPhase.Supported, pose.Phase);
            Assert.Equal(929, pose.Position.Y);
            Assert.Equal(1040, pose.Position.Y + foot.SoleY);
            Assert.Equal(42, pose.Support!.Value.Handle);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(87)]
    [InlineData(299)]
    public void Other_identity_errors_still_invalidate_scene_with_numeric_diagnostic(int error)
    {
        using var window = new OffscreenWindow();
        var reader = new DesktopMetadataReader(0, _ => throw new Win32Exception(error));
        var source = new DesktopSceneSource(new DesktopSceneNative(new WindowMetadata(reader, window.Handle)));
        Assert.Equal(SceneReadHealth.Expired, source.Read(TimeSpan.Zero).Health);
        Assert.Equal($"ProcessIdentity:{error}", source.LastFailure);
    }

    [Theory]
    [InlineData("Dororong.App.exe")]
    [InlineData("Dororong.exe")]
    public void Confirmed_other_pet_executable_remains_excluded(string executableName)
    {
        using var window = new OffscreenWindow();
        var reader = new DesktopMetadataReader(0, _ => $@"C:\AnotherPet\{executableName.ToUpperInvariant()}");
        var native = reader.ReadWindow(window.Handle);
        Assert.NotNull(native);
        Assert.True(native.Excluded);
        Assert.Equal((uint)Environment.ProcessId, native.ProcessId);
    }

    [Fact]
    public void Ordinary_application_executable_remains_eligible_for_platform_scene()
    {
        using var window = new OffscreenWindow();
        var reader = new DesktopMetadataReader(0, _ => @"C:\Apps\OrdinaryWpf.exe");

        var native = reader.ReadWindow(window.Handle);

        Assert.NotNull(native);
        Assert.False(native.Excluded);
        Assert.True(native.CanSupport);
        Assert.True(native.CanOcclude);
    }

    [Fact]
    public void Own_pet_handle_remains_excluded_when_identity_is_unavailable()
    {
        using var window = new OffscreenWindow();
        var reader = new DesktopMetadataReader(window.Handle, _ => throw new Win32Exception(5));
        Assert.True(reader.ReadWindow(window.Handle)!.Excluded);
    }

    [Fact]
    public void Non_identity_metadata_failure_is_not_downgraded_even_with_access_denied_code()
    {
        using var window = new OffscreenWindow();
        var reader = new DesktopMetadataReader(0, _ => throw new DesktopMetadataException("DwmFrame", 5));
        var source = new DesktopSceneSource(new DesktopSceneNative(new WindowMetadata(reader, window.Handle)));
        Assert.Equal(SceneReadHealth.Expired, source.Read(TimeSpan.Zero).Health);
        Assert.Equal("DwmFrame:5", source.LastFailure);
    }

    // Only desktop enumeration is deterministic here: the application window's PID,
    // visibility, class and DWM geometry come through the real native reader.
    private sealed class WindowMetadata(DesktopMetadataReader reader, nint handle) : IDesktopMetadataReader
    {
        public NativeDesktop Read() => new([new(1, new(0, 0, 1920, 1080))],
            [reader.ReadWindow(handle)!, new(42, 7, new(0, 1040, 1920, 40),
                Taskbar: true, HorizontalTaskbar: true, ClassName: "Shell_TrayWnd")]);
    }

    private sealed class OffscreenWindow : IDisposable
    {
        internal nint Handle { get; } = CreateWindowEx(0x08000080, "STATIC", "", 0x90000000,
            -30000, -30000, 320, 160, 0, 0, 0, 0); // NOACTIVATE | TOOLWINDOW; POPUP | VISIBLE

        internal OffscreenWindow() => Assert.NotEqual(0, Handle);
        public void Dispose() => DestroyWindow(Handle);

        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint CreateWindowEx(uint extendedStyle, string className, string name, uint style,
            int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(nint handle);
    }
}
