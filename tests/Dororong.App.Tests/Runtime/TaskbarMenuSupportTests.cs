using Dororong.App.Interop;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed class TaskbarMenuSupportTests
{
    private static readonly NativeWindow Bar = new(1,10,new(0,1040,1920,40),Taskbar:true,HorizontalTaskbar:true,ClassName:"Shell_TrayWnd");
    private static readonly NativeWindow Menu = new(2,10,new(1600,600,320,450),CanSupport:false,ClassName:"#32768");
    private sealed class Metadata : IDesktopMetadataReader
    {
        internal NativeWindow[] Windows=[Bar];
        public NativeDesktop? Read()=>new([new(1,new(0,0,1920,1080))],Windows);
    }
    private static PerchSurface[] Perches(DesktopScene scene)=>PlatformGeometry.Build(scene,0)
        .Select(s=>new PerchSurface(s,scene.Windows.FirstOrDefault(w=>w.Key==s.Key)?.ZOrder??int.MaxValue)).ToArray();

    [Fact]
    public void Repeated_shell_menu_open_close_preserves_right_side_support()
    {
        var reader=new Metadata();var native=new DesktopSceneNative(reader);native.Capture(TimeSpan.Zero);
        var motion=new PlatformMotion();var foot=new FootContact(20,45,111,48);var position=new PointD(1700,929);
        for(var step=1;step<=12;step++)
        {
            reader.Windows=step%2==0?[Menu,Bar]:[Bar];
            var scene=native.Capture(TimeSpan.FromMilliseconds(step*80))!;
            var pose=motion.Advance(new(TimeSpan.FromMilliseconds(80),position,position,foot,PlatformGeometry.Build(scene,63),false,true));
            Assert.Equal(PlatformPhase.Supported,pose.Phase);
            Assert.Equal(position,pose.Position);
            Assert.Equal(1,pose.Support!.Value.Handle);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Shell_menu_keeps_entering_and_attached_grip_but_not_a_missing_taskbar(bool attached)
    {
        var reader=new Metadata();var native=new DesktopSceneNative(reader);native.Capture(TimeSpan.Zero);
        var scene=native.Capture(TimeSpan.FromMilliseconds(80))!;
        var perch=new EdgePerch();var contact=new PerchContact(20,45,70,10);var position=new PointD(1700,975);
        Assert.True(perch.TryBegin(position,contact,Perches(scene),scene.Monitors,true));
        if(attached)position=perch.Advance(TimeSpan.FromMilliseconds(160),position,contact,Perches(scene),scene.Monitors,true).Position;
        reader.Windows=[Menu,Bar];scene=native.Capture(TimeSpan.FromMilliseconds(160))!;
        var pose=perch.Advance(TimeSpan.FromMilliseconds(160),position,contact,Perches(scene),scene.Monitors,true);
        Assert.Equal(EdgePerchPhase.Attached,pose.Phase);Assert.Equal(new PointD(1700,970),pose.Position);
        reader.Windows=[Menu];scene=native.Capture(TimeSpan.FromMilliseconds(240))!;
        Assert.Equal(EdgePerchPhase.None,perch.Advance(TimeSpan.FromMilliseconds(16),pose.Position,contact,Perches(scene),scene.Monitors,true).Phase);
    }

    [Theory]
    [InlineData("#32768",20,true)] // Another process's menu still cuts the bar.
    [InlineData("OrdinaryWindow",10,true)] // Same process alone is not enough.
    [InlineData("#32768",10,false)] // Shell menu still occludes ordinary windows.
    public void Real_window_and_unrelated_menu_occlusion_remain(string className,uint pid,bool taskbar)
    {
        var reader=new Metadata();var native=new DesktopSceneNative(reader);
        var surface=Bar with { Taskbar=taskbar,HorizontalTaskbar=taskbar,ClassName=taskbar?"Shell_TrayWnd":"CabinetWClass" };
        reader.Windows=[surface];native.Capture(TimeSpan.Zero);
        var scene=native.Capture(TimeSpan.FromMilliseconds(80))!;
        var motion=new PlatformMotion();var foot=new FootContact(20,45,111,48);var position=new PointD(1700,929);
        Assert.Equal(PlatformPhase.Supported,motion.Advance(new(TimeSpan.FromMilliseconds(16),position,position,foot,PlatformGeometry.Build(scene,63),false,true)).Phase);
        reader.Windows=[Menu with {ClassName=className,ProcessId=pid},surface];scene=native.Capture(TimeSpan.FromMilliseconds(160))!;
        Assert.Equal(PlatformPhase.Falling,motion.Advance(new(TimeSpan.FromMilliseconds(16),position,position,foot,PlatformGeometry.Build(scene,63),false,true)).Phase);
        Assert.DoesNotContain(PlatformGeometry.Build(scene,63),s=>s.Key.Handle==2);
    }
}
