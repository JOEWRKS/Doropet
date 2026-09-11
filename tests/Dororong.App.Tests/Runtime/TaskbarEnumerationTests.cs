using System.Runtime.InteropServices;
using Dororong.App.Interop;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public class TaskbarEnumerationTests
{
    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void Live_taskbar_missing_from_enum_snapshot_still_preserves_support_and_identity(string className)
    {
        using var bar = new TestWindow(className);
        var reader = new DesktopMetadataReader(0, _ => @"C:\Windows\explorer.exe");
        var metadata = new Metadata(reader, bar.Handle);
        var native = new DesktopSceneNative(metadata);
        native.Capture(TimeSpan.Zero);
        var scene = native.Capture(TimeSpan.FromMilliseconds(80))!;
        var key = Assert.Single(scene.Windows).Key;
        var motion = new PlatformMotion();
        var foot = new FootContact(63,72,111,47);
        var position = new PointD(400,929);
        for (var ms=160; ms<=1440; ms+=80)
        {
            metadata.Omitted = ms>=240 && ms<=1040;
            scene = native.Capture(TimeSpan.FromMilliseconds(ms))!;
            Assert.Equal(key, Assert.Single(scene.Windows).Key);
            var pose = motion.Advance(new(TimeSpan.FromMilliseconds(80),position,position,foot,
                PlatformGeometry.Build(scene,80),false,true));
            Assert.Equal(PlatformPhase.Supported,pose.Phase);
            Assert.Equal(929,pose.Position.Y);
        }
    }

    [Fact]
    public void Omitted_taskbar_keeps_current_z_order_below_a_real_occluder()
    {
        using var bar = new TestWindow("Shell_TrayWnd");
        using var app = new TestWindow("OrdinaryEnumerationWindow");
        var reader = new DesktopMetadataReader(0, _ => @"C:\Ordinary.exe");
        var windows = reader.ReadOrderedWindows([app.Handle],[app.Handle,bar.Handle]);
        Assert.Equal(new[]{app.Handle.ToInt64(),bar.Handle.ToInt64()},windows.Select(w=>w.Handle));
        var metadata = new FixedMetadata(windows.Select(w=>w with { Bounds = w.Taskbar ? new(0,1040,1920,40) : new(0,900,1000,180) }).ToArray());
        var native = new DesktopSceneNative(metadata);
        native.Capture(TimeSpan.Zero);
        var scene = native.Capture(TimeSpan.FromMilliseconds(80))!;
        var surface = Assert.Single(PlatformGeometry.Build(scene,80).Where(s=>s.Kind==PlatformKind.Taskbar));
        Assert.Equal(1000,surface.Left); Assert.Equal(1920,surface.Right);
    }

    [Fact]
    public void Unenumerated_ordinary_window_is_not_newly_admitted()
    {
        using var window = new TestWindow("OrdinaryEnumerationWindow");
        Assert.Empty(new DesktopMetadataReader(0).ReadOrderedWindows([], [window.Handle]));
    }

    [Fact]
    public void Live_taskbar_missing_both_lists_without_order_evidence_rejects_capture()
    {
        using var bar = new TestWindow("Shell_TrayWnd");
        var reader = new DesktopMetadataReader(0, _ => @"C:\Windows\explorer.exe");
        var metadata = new Metadata(reader,bar.Handle);
        var native = new DesktopSceneNative(metadata);
        var key = Assert.Single(native.Capture(TimeSpan.Zero)!.Windows).Key;
        metadata.Absent = true;
        var error=Assert.Throws<DesktopMetadataException>(()=>native.Capture(TimeSpan.FromMilliseconds(80)));
        Assert.StartsWith("TaskbarOrderUnavailable:",error.Diagnostic);
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void Known_live_taskbar_missing_both_lists_is_recovered_between_current_native_neighbors(string className)
    {
        using var back=new TestWindow("RecoveryBack");
        using var bar=new TestWindow(className);
        using var front=new TestWindow("RecoveryFront");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        reader.ReadOrderedWindows([front.Handle,bar.Handle,back.Handle],[front.Handle,bar.Handle,back.Handle]);
        bar.Move(-29900,-29950,1800,30);
        var windows=reader.ReadOrderedWindows([front.Handle,back.Handle],[front.Handle,back.Handle]);
        Assert.Equal(new[]{front.Handle.ToInt64(),bar.Handle.ToInt64(),back.Handle.ToInt64()},windows.Select(w=>w.Handle));
        var recovered=windows[1];
        Assert.Equal(new RectD(-29900,-29950,1800,30),recovered.Bounds);
        Assert.True(recovered.Visible);
        var native=new DesktopSceneNative(new FixedMetadata(windows.Select(w=>w with {
            Bounds=w.Taskbar?new(0,1040,1920,40):w.Handle==front.Handle?new(0,900,1000,180):new(1000,900,920,180)
        }).ToArray()));
        native.Capture(TimeSpan.Zero);
        var surface=Assert.Single(PlatformGeometry.Build(native.Capture(TimeSpan.FromMilliseconds(80))!,80).Where(s=>s.Kind==PlatformKind.Taskbar));
        Assert.Equal(1000,surface.Left); Assert.Equal(1920,surface.Right);
    }

    [Fact]
    public void Destroyed_previously_seen_taskbar_is_really_removed()
    {
        var bar=new TestWindow("Shell_TrayWnd");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        reader.ReadOrderedWindows([bar.Handle],[bar.Handle]);
        bar.Dispose();
        Assert.Empty(reader.ReadOrderedWindows([],[]));
    }

    [Fact]
    public void Hidden_previously_seen_taskbar_does_not_retain_visible_geometry()
    {
        using var bar=new TestWindow("Shell_TrayWnd");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        reader.ReadOrderedWindows([bar.Handle],[bar.Handle]);
        bar.Hide();
        var hidden=Assert.Single(reader.ReadOrderedWindows([],[]));
        Assert.False(hidden.Visible);
        var native=new DesktopSceneNative(new FixedMetadata([hidden]));
        Assert.Empty(PlatformGeometry.Build(native.Capture(TimeSpan.FromSeconds(1))!,80).Where(s=>s.Kind==PlatformKind.Taskbar));
    }

    [Fact]
    public void Conflicting_neighbor_order_rejects_capture_instead_of_putting_taskbar_on_top()
    {
        using var back=new TestWindow("RecoveryBack");
        using var bar=new TestWindow("Shell_TrayWnd");
        using var front=new TestWindow("RecoveryFront");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        reader.ReadOrderedWindows([front.Handle,bar.Handle,back.Handle],[front.Handle,bar.Handle,back.Handle]);
        var error=Assert.Throws<DesktopMetadataException>(()=>reader.ReadOrderedWindows([back.Handle,front.Handle],[back.Handle,front.Handle]));
        Assert.StartsWith("TaskbarOrderUnavailable:",error.Diagnostic);
    }

    [Fact]
    public void Hidden_primary_does_not_corrupt_recovered_secondary_taskbar_order()
    {
        using var back=new TestWindow("RecoveryBack");
        using var secondary=new TestWindow("Shell_SecondaryTrayWnd");
        using var primary=new TestWindow("Shell_TrayWnd");
        using var front=new TestWindow("RecoveryFront");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        reader.ReadOrderedWindows([front.Handle,primary.Handle,secondary.Handle,back.Handle],[front.Handle,primary.Handle,secondary.Handle,back.Handle]);
        primary.Hide();
        var windows=reader.ReadOrderedWindows([front.Handle,back.Handle],[front.Handle,back.Handle]);
        Assert.Equal(new[]{front.Handle.ToInt64(),secondary.Handle.ToInt64(),back.Handle.ToInt64()},windows.Where(w=>w.Visible).Select(w=>w.Handle));
        Assert.False(Assert.Single(windows.Where(w=>w.Handle==primary.Handle)).Visible);
    }

    [Fact]
    public void Both_list_omission_preserves_stable_support_for_longer_than_scene_expiry()
    {
        using var back=new TestWindow("RecoveryBack");
        using var bar=new TestWindow("Shell_TrayWnd");
        using var front=new TestWindow("RecoveryFront");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        bool omitted=false;
        var native=new DesktopSceneNative(new DelegateMetadata(()=>reader.ReadOrderedWindows(
            omitted?[front.Handle,back.Handle]:[front.Handle,bar.Handle,back.Handle],
            omitted?[front.Handle,back.Handle]:[front.Handle,bar.Handle,back.Handle])
            .Select(w=>w with{Bounds=w.Taskbar?new(0,1040,1920,40):new(0,0,100,100)}).ToArray()));
        native.Capture(TimeSpan.Zero);
        var key=Assert.Single(native.Capture(TimeSpan.FromMilliseconds(80))!.Windows.Where(w=>w.Taskbar)).Key;
        var motion=new PlatformMotion();
        var position=new PointD(400,929);
        for(int ms=160;ms<=1440;ms+=80)
        {
            omitted=ms>=240&&ms<=1040;
            var scene=native.Capture(TimeSpan.FromMilliseconds(ms))!;
            Assert.Equal(key,Assert.Single(scene.Windows.Where(w=>w.Taskbar)).Key);
            var pose=motion.Advance(new(TimeSpan.FromMilliseconds(80),position,position,new(63,72,111,47),PlatformGeometry.Build(scene,80),false,true));
            Assert.Equal(PlatformPhase.Supported,pose.Phase);
            Assert.Equal(929,pose.Position.Y);
        }
    }

    [Fact]
    public void Thin_autohide_strip_missing_both_lists_is_not_support()
    {
        using var back=new TestWindow("RecoveryBack");
        using var bar=new TestWindow("Shell_TrayWnd");
        using var front=new TestWindow("RecoveryFront");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        reader.ReadOrderedWindows([front.Handle,bar.Handle,back.Handle],[front.Handle,bar.Handle,back.Handle]);
        bar.Move(-30000,-30000,1920,2);
        var windows=reader.ReadOrderedWindows([front.Handle,back.Handle],[front.Handle,back.Handle]);
        Assert.Equal(2,Assert.Single(windows.Where(w=>w.Taskbar)).Bounds.Height);
        var native=new DesktopSceneNative(new FixedMetadata(windows.Select(w=>w.Taskbar?w with{Bounds=new(0,1078,1920,w.Bounds.Height)}:w).ToArray()));
        native.Capture(TimeSpan.Zero);
        Assert.Empty(PlatformGeometry.Build(native.Capture(TimeSpan.FromSeconds(2))!,80).Where(s=>s.Kind==PlatformKind.Taskbar));
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    public void First_capture_can_acquire_directly_discovered_bar_missing_both_lists(string className)
    {
        using var back=new TestWindow("RecoveryBack");
        using var bar=new TestWindow(className);
        using var front=new TestWindow("RecoveryFront");
        var reader=new DesktopMetadataReader(0,_=>@"C:\Windows\explorer.exe");
        var windows=reader.ReadOrderedWindows([front.Handle,back.Handle],[front.Handle,back.Handle],[bar.Handle]);
        Assert.Equal(new[]{front.Handle.ToInt64(),bar.Handle.ToInt64(),back.Handle.ToInt64()},windows.Select(w=>w.Handle));
    }

    [Fact]
    public void Hidden_taskbar_is_not_promoted_to_visible_support_by_enum_fallback()
    {
        using var bar = new TestWindow("Shell_TrayWnd");
        bar.Hide();
        var reader=new DesktopMetadataReader(0);
        var hidden=Assert.Single(reader.ReadOrderedWindows([], [bar.Handle]));
        Assert.False(hidden.Visible);
        var native=new DesktopSceneNative(new FixedMetadata([hidden]));
        native.Capture(TimeSpan.Zero);
        var scene=native.Capture(TimeSpan.FromSeconds(1))!;
        Assert.Empty(PlatformGeometry.Build(scene,80).Where(s=>s.Kind==PlatformKind.Taskbar));
    }

    [Fact]
    public void Destroyed_taskbar_handle_is_not_recovered()
    {
        var bar=new TestWindow("Shell_TrayWnd");
        var handle=bar.Handle; bar.Dispose();
        Assert.Empty(new DesktopMetadataReader(0).ReadOrderedWindows([], [handle]));
    }

    [Fact]
    public void Live_enumerated_window_missing_from_z_order_still_rejects_inconsistent_capture()
    {
        using var bar = new TestWindow("Shell_TrayWnd");
        var error=Assert.Throws<DesktopMetadataException>(()=>new DesktopMetadataReader(0).ReadOrderedWindows([bar.Handle],[]));
        Assert.StartsWith("ZOrderChanged:",error.Diagnostic);
    }

    [Fact]
    public void Repeated_z_order_handle_is_still_rejected()
    {
        var error=Assert.Throws<DesktopMetadataException>(()=>new DesktopMetadataReader(0).ReadOrderedWindows([],[(nint)123,(nint)123]));
        Assert.StartsWith("ZOrderTraversal:",error.Diagnostic);
    }

    private sealed class FixedMetadata(NativeWindow[] windows) : IDesktopMetadataReader
    { public NativeDesktop Read()=>new([new(1,new(0,0,1920,1080))],windows); }
    private sealed class DelegateMetadata(Func<NativeWindow[]> read) : IDesktopMetadataReader
    { public NativeDesktop Read()=>new([new(1,new(0,0,1920,1080))],read()); }

    private sealed class Metadata(DesktopMetadataReader reader,nint handle) : IDesktopMetadataReader
    {
        internal bool Omitted,Absent;
        public NativeDesktop Read()
        {
            var windows=reader.ReadOrderedWindows(Omitted||Absent?[]:[handle],Absent?[]:[handle]);
            return new([new(1,new(0,0,1920,1080))],windows.Select(w=>w with {Bounds=new(0,1040,1920,40)}).ToArray());
        }
    }

    private sealed class TestWindow : IDisposable
    {
        private readonly string className;
        private static readonly WindowProcedure Procedure=DefWindowProc;
        private readonly nint instance=GetModuleHandle(null);
        internal nint Handle { get; }
        internal TestWindow(string className)
        {
            this.className=className;
            var definition=new WindowClass { Procedure=Marshal.GetFunctionPointerForDelegate(Procedure),Instance=instance,ClassName=className };
            Assert.NotEqual(0,RegisterClass(ref definition));
            Handle=CreateWindowEx(0x08000080,className,"",0x90000000,-30000,-30000,1920,40,0,0,instance,0);
            Assert.NotEqual(0,Handle);
        }
        public void Dispose(){DestroyWindow(Handle);UnregisterClass(className,instance);}
        internal void Hide()=>ShowWindow(Handle,0);
        internal void Move(int x,int y,int width,int height)=>Assert.True(SetWindowPos(Handle,0,x,y,width,height,0x0014));
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private struct WindowClass
        {
            internal uint Style; internal nint Procedure; internal int ClassExtra,WindowExtra;
            internal nint Instance,Icon,Cursor,Background; internal string? MenuName; internal string ClassName;
        }
        private delegate nint WindowProcedure(nint window,uint message,nint wParam,nint lParam);
        [DllImport("user32.dll",EntryPoint="RegisterClassW",CharSet=CharSet.Unicode)] private static extern ushort RegisterClass(ref WindowClass definition);
        [DllImport("user32.dll",EntryPoint="UnregisterClassW",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterClass(string className,nint instance);
        [DllImport("user32.dll",EntryPoint="DefWindowProcW")] private static extern nint DefWindowProc(nint window,uint message,nint wParam,nint lParam);
        [DllImport("kernel32.dll",EntryPoint="GetModuleHandleW",CharSet=CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
        [DllImport("user32.dll",EntryPoint="CreateWindowExW",CharSet=CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex,string cls,string name,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint handle);
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint handle,int command);
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint handle,nint after,int x,int y,int width,int height,uint flags);
    }
}
