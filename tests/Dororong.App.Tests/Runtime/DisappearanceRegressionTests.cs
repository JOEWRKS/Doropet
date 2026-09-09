using Dororong.App.Controls;
using Dororong.App.Interop;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using System.Windows.Controls;

namespace Dororong.App.Tests.Runtime;

public class DisappearanceRegressionTests
{
    [Fact]
    public void Rebase_selects_monitor_from_preserved_world_center_not_old_host_origin()
    {
        var pinned=true;
        var native=new StackedNative();
        var runtime=new PetPlatformRuntime(new(native),_=>new(new(),new(),1,1),
            ()=>pinned?(new FootContact(63,72,127,64),new RectD(28,64,72,63)):(new FootContact(63,72,111,48),new RectD(28,48,72,63)),
            (_,_)=>pinned=false);
        var displayed=new PointD(400,-93);
        runtime.BeginFrame(TimeSpan.Zero,displayed,PointerSample.Unavailable,false,false);
        runtime.BeginFrame(TimeSpan.FromMilliseconds(16),displayed,PointerSample.Unavailable,false,true);
        Assert.Equal(-77,runtime.FramePosition.Y);
        var area=runtime.GetMovementArea(runtime.FramePosition,runtime.Contact,new(0,0));
        Assert.Equal(969,area.Bottom); // lower monitor1080 minus localsole111
    }

    [Fact]
    public void Rest_rebase_keeps_squashed_contact_on_narrow_window_edge() => Controls.CheekProductTests.Sta(()=>
    {
        var p=new DororongPresenter();p.Render(new(PetState.Idle,default,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);
        p.Measure(new(144,144));p.Arrange(new(0,0,144,144));p.UpdateLayout();
        p.ApplyPlatformPose(new(PlatformPhase.Supported,default,null,0,0),161);
        var native=new NarrowNative();var position=new PointD(400,300);var time=TimeSpan.Zero;
        var runtime=new PetPlatformRuntime(new(native),_=>new(new(),new(),1,1),p.MeasurePlatformGeometry,p.ApplyPlatformPose);
        PlatformPose Step(bool rest,int clockMs=16){
            time+=TimeSpan.FromMilliseconds(clockMs);
            runtime.BeginFrame(time,position,PointerSample.Unavailable,false,rest);
            position=runtime.FramePosition;var sole=runtime.Contact.SoleY;
            var pose=runtime.Advance(time,TimeSpan.FromMilliseconds(16),position,position,runtime.Contact,false);position=pose.Position;
            p.Render(new(PetState.Idle,position,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);p.UpdateLayout();runtime.AfterRender(pose,sole);return pose;
        }
        var pose=Step(false);for(var i=0;pose.Phase!=PlatformPhase.Landing&&i<100;i++)pose=Step(false);
        Assert.Equal(PlatformPhase.Landing,pose.Phase);
        for(var i=0;i<4;i++)pose=Step(false);
        Assert.True(pose.Squash>.25);
        Assert.True(464.6-position.X-runtime.Contact.Left>=2);
        native.Right=464.6;
        pose=Step(true,80);
        Assert.Equal(PlatformPhase.Landing,pose.Phase);
        Assert.NotNull(pose.Support);
    });

    [Theory]
    [InlineData("TaskListThumbnailWnd",true)]
    [InlineData("TaskListOverlayWnd",true)]
    [InlineData("tooltips_class32",true)]
    [InlineData("SysShadow",true)]
    [InlineData("#32768",false)]
    [InlineData("SomeOtherApplicationWindow",false)]
    public void Shell_preview_crossing_bar_does_not_remove_support_but_ordinary_window_does(string className,bool preview)
    {
        var reader=new Metadata();var native=new DesktopSceneNative(reader);
        var bar=new NativeWindow(1,10,new(0,1040,1920,40),Taskbar:true,HorizontalTaskbar:true,ClassName:"Shell_TrayWnd");
        reader.Windows=[bar];native.Capture(TimeSpan.Zero);
        var scene=native.Capture(TimeSpan.FromMilliseconds(80))!;
        var motion=new PlatformMotion();var foot=new FootContact(20,45,111,48);var position=new PointD(400,929);
        var pose=motion.Advance(new(TimeSpan.FromMilliseconds(16),position,position,foot,PlatformGeometry.Build(scene,63),false,true));
        Assert.Equal(PlatformPhase.Supported,pose.Phase);
        reader.Windows=[new(2,10,new(0,830,1920,211),CanSupport:className is not "tooltips_class32" and not "#32768",ClassName:className),bar];
        scene=native.Capture(TimeSpan.FromMilliseconds(160))!;
        pose=motion.Advance(new(TimeSpan.FromMilliseconds(16),position,position,foot,PlatformGeometry.Build(scene,63),false,true));
        Assert.Equal(preview?PlatformPhase.Supported:PlatformPhase.Falling,pose.Phase);
        if(preview){Assert.Equal(929,pose.Position.Y);Assert.False(scene.Windows[0].CanSupport);Assert.False(scene.Windows[0].CanOcclude);}
    }

    [Fact]
    public void Tooltip_and_separate_shadow_preserve_support_through_repeated_hover()
    {
        var reader=new Metadata(); var native=new DesktopSceneNative(reader);
        var bar=new NativeWindow(196818,8556,new(0,1040,1920,40),Taskbar:true,HorizontalTaskbar:true,ClassName:"Shell_TrayWnd");
        // Captured primary-monitor hover geometry: tooltip plus a distinct,
        // five-pixel-larger decoration, both ahead of the same unchanged bar.
        var tooltip=new NativeWindow(65890,8556,new(1707,1039,93,20),CanSupport:false,ClassName:"tooltips_class32");
        var shadow=new NativeWindow(48893604,8556,new(1707,1039,98,25),ClassName:"SysShadow");
        reader.Windows=[bar]; native.Capture(TimeSpan.Zero);
        var motion=new PlatformMotion(); var position=new PointD(1648,929);
        var foot=new FootContact(62.95,72.16,111,47.25);
        for(var step=1;step<=16;step++)
        {
            // Include the shadow's independent last frame after tooltip closes.
            reader.Windows=(step%4) switch { 2=>[tooltip,shadow,bar],3=>[shadow,bar],_=>[bar] };
            var scene=native.Capture(TimeSpan.FromMilliseconds(step*80))!;
            var pose=motion.Advance(new(TimeSpan.FromMilliseconds(80),position,position,foot,PlatformGeometry.Build(scene,63.75),false,true));
            Assert.Equal(PlatformPhase.Supported,pose.Phase);
            Assert.Equal(196818,pose.Support!.Value.Handle);
            Assert.Equal(1040,pose.Position.Y+foot.SoleY);
            Assert.Equal(929,pose.Position.Y);
        }
    }

    [Fact]
    public void Repeated_head_release_restores_local_frame_without_moving_world_foot() => Controls.CheekProductTests.Sta(()=>
    {
        var p=new DororongPresenter();var grid=new Grid{Width=336,Height=336};grid.Children.Add(p);
        p.HorizontalAlignment=System.Windows.HorizontalAlignment.Center;p.VerticalAlignment=System.Windows.VerticalAlignment.Center;
        grid.Measure(new(336,336));grid.Arrange(new(0,0,336,336));grid.UpdateLayout();
        var position=new PointD(400,929);var elapsed=TimeSpan.Zero;EventHandler? tick=null;
        var pointer=PointerSample.Unavailable;bool down=false;DirectInteractionSnapshot direct=default;PetSnapshot snapshot=default;
        var runtime=new PetPlatformRuntime(new(new SceneNative()),_=>new(new(),new(),1,1),p.MeasurePlatformGeometry,p.ApplyPlatformPose);
        var host=new PetLoopHost(()=>new(0,0,1920,1080),()=>new(144,144),()=>new(4,4),()=>pointer,()=>down,()=>position,pos=>position=pos,
            (s,d)=>{snapshot=s;direct=d;p.Render(s,d);grid.UpdateLayout();},()=>true,()=>{}){Platforms=runtime};
        using var loop=new PetLoop(new(()=>elapsed,()=>{},()=>{}),new(h=>tick+=h,h=>tick-=h,()=>{},()=>{}),host,
            _=>new(BehaviorTuning.Default with{IdleMin=TimeSpan.FromMinutes(10),IdleMax=TimeSpan.FromMinutes(10)},new SeededRandomSource(1),position));
        Exception? fault=null;loop.Faulted+=(_,e)=>fault=e;loop.Start();
        void Tick(int count){for(var i=0;i<count;i++){elapsed+=TimeSpan.FromMilliseconds(16);tick?.Invoke(null,EventArgs.Empty);Assert.Null(fault);}}
        Tick(10);
        for(var cycle=0;cycle<4;cycle++)
        {
            var image=(Image)p.FindName("DororongImage");var pt=image.TranslatePoint(new(40,30),p);var local=new PointD(pt.X,pt.Y);
            Assert.InRange(local.Y+96,0,335); // The press remains inside the real host, not a clipped synthetic regrab.
            var origin=position+local;pointer=new(true,origin);down=true;
            loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.Body,local,new(40,30),-1));Tick(1);
            for(var i=1;i<=20;i++){pointer=new(true,origin+new PointD(0,-i*11));Tick(1);}
            down=false;Tick(70);
            Assert.Equal(DirectInteractionTarget.None,direct.Target);
            Assert.InRange(runtime.Contact.SoleY,110,112);
            Assert.Equal(1040,position.Y+runtime.Contact.SoleY,5);
            Assert.Equal(position,snapshot.Position);
            Assert.InRange(p.MeasurePlatformGeometry()!.Value.Bounds.Y+96,0,240);
        }
    });

    private sealed class Metadata:IDesktopMetadataReader
    {
        internal NativeWindow[] Windows=[];
        public NativeDesktop? Read()=>new([new(1,new(0,0,1920,1080))],Windows);
    }
    private sealed class SceneNative:IDesktopSceneNative
    {
        public DesktopScene? Capture(TimeSpan now)=>new(1,now,[new(1,new(0,0,1920,1080))],
            [new(new(1,10,1),new(0,1040,1920,40),2,true,false,false,false,true,true,true,true)]);
    }
    private sealed class StackedNative:IDesktopSceneNative
    {
        public DesktopScene? Capture(TimeSpan now)=>new(1,now,[new(1,new(0,-1080,1920,1080)),new(2,new(0,0,1920,1080))],[]);
    }
    private sealed class NarrowNative:IDesktopSceneNative
    {
        internal double Right=1920;
        public DesktopScene? Capture(TimeSpan now)=>new(1,now,[new(1,new(0,0,1920,1080))],
            [new(new(1,10,1),new(0,800,Right,200),0,true,false,false,false,true,true,false,false)]);
    }
}
