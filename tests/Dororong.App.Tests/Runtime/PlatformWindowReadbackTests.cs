using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dororong.App.Runtime;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public class PlatformWindowReadbackTests
{
    // Catches resets caused by actual HWND rounding, not just ideal in-memory positions.
    [Theory]
    [InlineData(false,100)] [InlineData(true,100)]
    [InlineData(false,498.37)] [InlineData(true,498.37)]
    public void Own_window_pixel_readback_preserves_acceleration_and_touchdown(bool actualMap,double startY) => Controls.CheekProductTests.Sta(()=>
    {
        var grid=new Grid{Width=144,Height=144};
        var window=new Window{Width=336,Height=336,Content=grid,WindowStyle=WindowStyle.None,AllowsTransparency=true,Opacity=0,ShowActivated=false,ShowInTaskbar=false,Left=304,Top=startY-96};
        try
        {
            window.Show();window.UpdateLayout();
            var contact=new FootContact(40,65,100,20);
            var runtime=new PetPlatformRuntime(new(new Native()),p=>actualMap?DesktopCoordinateMap.FromPresenter(grid,p):new(new(),new(),1,1),()=>(contact,new RectD(20,20,70,80)),(_,_)=>{});
            int? firstLanding=null;int landingTicks=0;double peak=0;
            for(var i=0;i<100;i++)
            {
                window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                var displayed=new PointD(window.Left+96,window.Top+96);
                var time=TimeSpan.FromMilliseconds(i*16);
                runtime.BeginFrame(time,displayed,PointerSample.Unavailable,false);
                var pose=runtime.Advance(time,TimeSpan.FromMilliseconds(16),displayed,displayed,contact,false);
                if(pose.Phase==PlatformPhase.Landing){firstLanding??=i;landingTicks++;}
                peak=Math.Max(peak,Math.Abs(pose.Squash));
                window.Left=pose.Position.X-96;window.Top=pose.Position.Y-96;window.UpdateLayout();
                runtime.AfterRender(pose,contact.SoleY);
            }
            Assert.NotNull(firstLanding);
            Assert.InRange(firstLanding.Value,0,startY<200?45:8);
            Assert.InRange(landingTicks,28,30);
            Assert.True(peak>(startY<200?.25:.03),$"No height-scaled landing squash: {peak}");
            Assert.InRange(window.Top+96,499,501);
        }
        finally{window.Close();}
    });

    private sealed class Native:IDesktopSceneNative
    {
        public DesktopScene? Capture(TimeSpan now)=>new(1,now,[new(1,new(0,0,1920,600))],[]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Real_presenter_loop_reaches_floor_without_facing_flip_and_renders_squash(bool mirror) => Controls.CheekProductTests.Sta(()=>
    {
        var presenter=new DororongPresenter{Width=144,Height=144};
        var window=new Window{Content=presenter,WindowStyle=WindowStyle.None,AllowsTransparency=true,Opacity=0,ShowActivated=false,ShowInTaskbar=false,Left=304,Top=4};
        var viewport=new PetWindowViewport(window,presenter);PetLoop? loop=null;
        try
        {
            window.Show();window.UpdateLayout();
            TimeSpan elapsed=TimeSpan.Zero;EventHandler? tick=null;Exception? fault=null;
            PetSnapshot last=default;PlatformPose? lastPose=null;double minHeight=double.PositiveInfinity;double initialHeight=0;int landings=0;int? firstLanding=null;
            var runtime=new PetPlatformRuntime(new(new Native()),p=>DesktopCoordinateMap.FromPresenter(presenter,p),()=>presenter.MeasurePlatformGeometry(),
                (pose,sole)=>{lastPose=pose;presenter.ApplyPlatformPose(pose,sole);if(pose?.Phase==PlatformPhase.Landing){landings++;minHeight=Math.Min(minHeight,presenter.MeasurePlatformGeometry()!.Value.Bounds.Height);}});
            var host=new PetLoopHost(()=>new(0,0,1920,600),viewport.GetPetSize,()=>new(4,4),()=>PointerSample.Unavailable,()=>false,viewport.GetPosition,viewport.SetPosition,
                (snapshot,direct)=>{last=snapshot;presenter.Render(snapshot,direct);window.UpdateLayout();},()=>true,()=>{}){Platforms=runtime};
            loop=new(new(()=>elapsed,()=>{},()=>{}),new(h=>tick+=h,h=>tick-=h,()=>{},()=>{}),host,_=>
            {
                var brain=new PetBrain(BehaviorTuning.Default with{IdleMin=TimeSpan.FromMilliseconds(100),IdleMax=TimeSpan.FromMilliseconds(100),IdleToWalkProbability=1},new FixedRandom(mirror?.5:0),new(400,100));
                if(mirror)brain.Update(new(TimeSpan.FromMilliseconds(100),new(0,0,1920,600),new(144,144),PointerSample.Unavailable,false,false,null,new(4,4)));
                return brain;
            });
            loop.Faulted+=(_,error)=>fault=error;loop.Start();
            initialHeight=presenter.MeasurePlatformGeometry()!.Value.Bounds.Height;
            var initialFacing=last.Facing;
            for(var i=0;i<100;i++)
            {
                window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                elapsed+=TimeSpan.FromMilliseconds(16);tick?.Invoke(null,EventArgs.Empty);
                Assert.Null(fault);
                if(lastPose?.Phase==PlatformPhase.Falling)Assert.Equal(initialFacing,last.Facing);
                if(lastPose?.Phase==PlatformPhase.Landing)firstLanding??=i;
            }
            Assert.NotNull(firstLanding);Assert.InRange(firstLanding.Value,0,48);
            Assert.InRange(landings,28,30);Assert.True(minHeight<initialHeight*.75);
            var contact=presenter.MeasurePlatformGeometry()!.Value.Contact;
            Assert.InRange(viewport.GetPosition().Y+contact.SoleY,599,601);
        }
        finally{loop?.Dispose();window.Close();}
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Genuine_external_move_or_scale_change_still_resets_from_actual_position(bool scaleChange)
    {
        var map=new DesktopCoordinateMap(new(),new(),1,1);var native=new MutableNative();var c=new FootContact(40,65,100,20);
        var runtime=new PetPlatformRuntime(new(native),_=>map,()=>(c,new RectD(20,20,70,80)),(_,_)=>{});
        var p=new PointD(100,100);
        for(var i=0;i<10;i++){var t=TimeSpan.FromMilliseconds(i*16);runtime.BeginFrame(t,p,PointerSample.Unavailable,false);p=runtime.Advance(t,TimeSpan.FromMilliseconds(16),p,p,c,false).Position;}
        if(scaleChange){map=new(new(),new(),2,2);native.Scale=2;}else p=new(300,250);
        var actual=p;runtime.BeginFrame(TimeSpan.FromMilliseconds(160),p,PointerSample.Unavailable,false);
        var next=runtime.Advance(TimeSpan.FromMilliseconds(160),TimeSpan.FromMilliseconds(16),p,p,c,false);
        Assert.Equal(actual.X,next.Position.X,6);Assert.Equal(actual.Y+.2304,next.Position.Y,6);
    }
    private sealed class MutableNative:IDesktopSceneNative
    {
        internal double Scale=1;
        public DesktopScene? Capture(TimeSpan now)=>new(1,now,[new(1,new(0,0,1920*Scale,600*Scale))],[]);
    }
    private sealed class FixedRandom(double value):IRandomSource{public double NextUnit()=>value;}
}
