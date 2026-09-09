using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Active_carry_matches_legacy_controller_while_support_moves_and_disappears(int target) => Controls.CheekProductTests.Sta(()=>
    {
        using var enabled=new Harness();using var legacy=new Harness(platformEnabled:false);
        enabled.Start();legacy.Start();enabled.Tick();legacy.Tick();enabled.Press(target);legacy.Press(target);enabled.Tick();legacy.Tick();
        for(var i=0;i<24;i++)
        {
            var pointer=new PointerSample(true,new(220+(i<12?i:-i),180-i));
            enabled.Pointer=legacy.Pointer=pointer;
            enabled.Native.Scene=i<12?Scene(120,170):Scene(windows:false);
            enabled.Tick();legacy.Tick();
            Assert.Equal(legacy.Position,enabled.Position);Assert.Equal(legacy.Core,enabled.Core);
            Assert.Equal(legacy.Captures,enabled.Captures);Assert.Null(enabled.LastPose);
        }
        enabled.Down=legacy.Down=false;enabled.Tick();legacy.Tick();
        var settleTicks=0;
        while(legacy.Direct.Target!=DirectInteractionTarget.None && settleTicks++<100)
        {
            enabled.Tick();legacy.Tick();Assert.NotNull(enabled.LastPose);Assert.Null(enabled.Direct.HeadLanding);
        }
        Assert.InRange(settleTicks,0,99);
        Assert.Equal(legacy.Releases,enabled.Releases);
    });

    [Fact]
    public void Pointer_carry_selects_destination_monitor_and_full_body_clamp_without_bridging_gap()
    {
        using var h=new Harness();h.Start();h.Tick();h.Press(0);h.Tick();
        h.Native.Scene=new(2,TimeSpan.Zero,[new(1,new(0,0,800,600)),new(2,new(1000,0,800,600))],[]);
        h.Pointer=new(true,new(1800,300));h.Tick(80);
        Assert.Equal(new PointD(1690,250),h.Position);
        Assert.Equal(h.Position,h.Core.Position);
    }
    [Fact]
    public void Platform_start_and_ticks_never_use_primary_legacy_work_area()
    {
        using var h=new Harness(refuseLegacyArea:true);h.Start();Assert.Null(h.Fault);h.Tick();
        Assert.Equal(new PointD(100,100),h.Position);
    }

    [Fact]
    public void Monitor_fallback_collection_is_bounded_during_repeated_scene_failure()
    {
        var reads=0;
        using var h=new Harness(fallback:()=>{reads++;return [new(1,new(0,0,800,600))];});
        h.Native.Fail=true;h.Start();var first=reads;
        for(var i=0;i<4;i++)h.Tick();Assert.Equal(first,reads);
        h.Tick();Assert.Equal(first+1,reads);
    }
    [Theory]
    [InlineData(0,40,30)] [InlineData(1,23,77)] [InlineData(2,43,83)]
    [InlineData(3,66,81)] [InlineData(4,53,70)] [InlineData(5,70,61)] [InlineData(6,16,58)]
    public void Real_presenter_loop_captures_corrected_pose_and_hands_off_last_canonical_sole(int target,double x,double y) => Controls.CheekProductTests.Sta(()=>
    {
        using var h=new Harness(realPresenter:true);h.Start();
        var sole=h.Presenter!.MeasurePlatformGeometry()!.Value.Contact.SoleY;
        h.Native.Scene=Scene(y:100+sole);h.Tick(80);
        h.Native.Scene=Scene(120,70+sole);h.Tick(80);
        Assert.Equal(new PointD(120,70),h.Position);
        Assert.Equal(sole,h.Presenter.MeasurePlatformContact()!.Value.SoleY,6);
        h.PressReal(target,new(x,y));h.Tick();var held=h.Position;
        h.Native.Scene=Scene(windows:false);h.Tick(80);Assert.Equal(held,h.Position);Assert.Null(h.LastPose);
        h.Down=false;h.Tick();
        var count=0;
        while(h.Direct.Target!=DirectInteractionTarget.None && count++<60)h.Tick();
        Assert.InRange(count,0,59);Assert.NotNull(h.LastPose);
        var releasePosition=h.Position;
        h.Tick();
        Assert.True(h.Position.Y>=releasePosition.Y);
        Assert.True(h.Position.Y+h.Presenter.MeasurePlatformContact()!.Value.SoleY<=600.001);
        Assert.Equal(h.Position,h.Core.Position);Assert.Equal(1,h.WritesThisTick);
    });
    [Fact]
    public void Native_monitor_only_fallback_returns_finite_real_monitor_bounds()
    {
        var monitors=Interop.DesktopMetadataReader.ReadMonitorBounds();
        Assert.NotEmpty(monitors);
        Assert.All(monitors,m=>{Assert.True(double.IsFinite(m.Bounds.X));Assert.True(double.IsFinite(m.Bounds.Y));Assert.True(m.Bounds.Width>0);Assert.True(m.Bounds.Height>0);});
    }

    [Fact]
    public void Startup_failed_scene_uses_independent_monitor_floor()
    {
        using var h=new Harness(fallback:()=>[new(1,new(0,0,800,600))]);h.Native.Fail=true;h.Start();
        for(var i=0;i<60;i++)h.Tick(80);
        Assert.Equal(new PointD(100,500),h.Position);
    }

    [Fact]
    public void Head_release_hands_off_immediately_and_loss_capture_resumes_from_displayed()
    {
        using var h=new Harness();h.Start();h.Tick();h.Press(0);h.Tick();
        h.Pointer=new(true,new(220,120));h.Tick();Assert.Equal(PetState.Dragged,h.Core.State);
        h.Native.Scene=Scene(windows:false);h.Down=false;h.Tick(80);
        Assert.Null(h.Direct.HeadLanding);Assert.Equal(PlatformPhase.Falling,h.LastPose?.Phase);
        var steps=0;
        while(h.Direct.Target!=DirectInteractionTarget.None && steps++<60){h.Tick();Assert.NotNull(h.LastPose);}
        Assert.InRange(steps,1,59);var landed=h.Position;h.Tick();Assert.True(h.Position.Y>landed.Y);Assert.NotNull(h.LastPose);
        h.Press(0);h.Tick();h.Pointer=new(true,h.Pointer.Position+new PointD(30,-30));h.Tick();
        h.Loop.NotifyDirectInteractionCanceled();var canceled=h.Position;Assert.Null(h.LastPose);
        h.Tick();Assert.Equal(canceled.X,h.Position.X);Assert.InRange(h.Position.Y,canceled.Y+.001,canceled.Y+1);
    }

    [Fact]
    public void Quick_release_and_immediate_regrab_do_not_apply_stale_support_delta()
    {
        using var h=new Harness();h.Start();h.Tick();h.Press(0);h.Down=false;
        h.Native.Scene=Scene(120,170);h.Tick(80);Assert.Equal(100,h.Position.X);Assert.InRange(h.Position.Y,100,106);Assert.NotNull(h.LastPose);
        var released=h.Position;
        h.Press(0);h.Tick();Assert.Equal(released,h.Position);
        Assert.True(h.Core.IsDirectInteractionPending);Assert.Null(h.LastPose);
    }
    [Fact]
    public void Moving_support_moves_host_and_brain_once_without_next_tick_snapback()
    {
        using var h=new Harness(); h.Start(); h.Tick();
        h.Native.Scene=Scene(120,170); h.Tick(80);
        Assert.Equal(new PointD(120,70),h.Position);
        Assert.Equal(h.Position,h.Core.Position);
        Assert.Equal(1,h.WritesThisTick);
        h.Tick(); Assert.Equal(new PointD(120,70),h.Position);
    }

    [Fact]
    public void Actual_OS_position_is_reconciled_before_autonomy_and_loss_falls_from_it()
    {
        using var h=new Harness(); h.Start(); h.Tick();
        h.Position=new(300,250);h.Native.Scene=Scene(windows:false);h.Tick(80);
        Assert.Equal(300,h.Position.X); Assert.InRange(h.Position.Y,250.001,256);
        Assert.Equal(h.Position,h.Core.Position);
    }

    [Fact]
    public void Expired_null_scene_retains_monitor_floor_and_eventually_lands()
    {
        using var h=new Harness(); h.Start(); h.Tick(); h.Native.Fail=true;
        h.Tick(80); Assert.Equal(new PointD(100,100),h.Position);
        for(var i=0;i<60;i++) h.Tick(80);
        Assert.Equal(new PointD(100,500),h.Position);
        Assert.Equal(h.Position,h.Core.Position);
    }

    [Fact]
    public void Removed_monitor_clamps_full_body_to_survivor_not_foot_width_or_gap()
    {
        using var h=new Harness(); h.Start(); h.Tick();
        h.Position=new(1500,100);
        h.Native.Scene=new(2,TimeSpan.Zero,[new(2,new(-800,-100,600,500))],[]);
        h.Tick(80);
        Assert.Equal(-310,h.Position.X); // survivor right -200 minus full right110
        Assert.InRange(h.Position.Y,-110,300);
        Assert.Equal(h.Position,h.Core.Position);
    }

    [Fact]
    public void Physical_pointer_uses_origin_anchored_map_before_head_carry()
    {
        using var h=new Harness();h.Start();h.Tick();h.Press(0);h.Tick();
        h.Map=new(new(1000,500),new(100,100),2,2);
        h.Native.Scene=new(2,TimeSpan.Zero,[new(1,new(800,300,1600,1200))],[]);
        h.Pointer=new(true,new(999,999)){ScreenPixelPosition=new(1160,620)};
        h.Tick(80);
        Assert.Equal(new PointD(120,110),h.Position);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Direct_head_five_body_regions_and_captured_cheek_hold_position_when_support_moves_or_disappears(int target) => Controls.CheekProductTests.Sta(()=>
    {
        using var h=new Harness();h.Start();h.Tick();h.Press(target);h.Tick();
        var held=h.Position;h.Native.Scene=Scene(120,170);h.Tick(80);
        Assert.Equal(held,h.Position); Assert.Null(h.LastPose);
        h.Native.Scene=Scene(windows:false);h.Tick(80); Assert.Equal(held,h.Position);
        Assert.Equal(h.Position,h.Core.Position);Assert.True(h.Core.IsDirectInteractionPending || h.Captures==1);
        h.Down=false;h.Tick();
        // Even zero-distance release hands control straight to the surface solver.
        Assert.Equal(held.X,h.Position.X);Assert.True(h.Position.Y>held.Y);
        var ended=false;
        for(var i=0;i<50;i++)
        {
            var wasActive=h.Direct.Target!=DirectInteractionTarget.None || h.Direct.HeadLanding is not null;
            h.Tick();
            if(wasActive) Assert.NotNull(h.LastPose);
            else { ended=true;break; }
        }
        Assert.True(ended);Assert.NotNull(h.LastPose);
    });

    [Theory]
    [InlineData("cancel")] [InlineData("dispose")] [InlineData("fault")]
    public void Cleanup_clears_platform_layer_and_release_is_not_duplicated(string action) => Controls.CheekProductTests.Sta(()=>
    {
        using var h=new Harness();h.Start();h.Tick();Assert.NotNull(h.LastPose);
        h.Press(6);h.Tick();Assert.Equal(1,h.Captures);
        if(action=="cancel")h.Loop.NotifyDirectInteractionCanceled();
        else if(action=="dispose")h.Dispose();
        else {h.ThrowWrite=true;h.Tick();Assert.NotNull(h.Fault);}
        Assert.Null(h.LastPose);Assert.Equal(1,h.Releases);
        h.Dispose();Assert.Equal(1,h.Releases);
    });

    private static DesktopScene Scene(double x=100,double y=200,bool windows=true) => new(1,TimeSpan.Zero,
        [new(1,new(0,0,800,600))],windows ? [new(new(1,1,1),new(x,y,500,300),0,true,false,false,false,true,true,false,false)] : []);

    private sealed class Native : IDesktopSceneNative
    {
        internal DesktopScene Scene=PetLoopPlatformTests.Scene();internal bool Fail;
        public DesktopScene? Capture(TimeSpan now)=>Fail?null:Scene with {CapturedAt=now};
    }

    private sealed class Harness : IDisposable
    {
        internal readonly Native Native=new();internal readonly PetLoop Loop;
        internal readonly PetPlatformRuntime Platforms;
        internal DororongPresenter? Presenter;
        internal PointD Position=new(100,100);internal PointerSample Pointer=PointerSample.Unavailable;
        internal DesktopCoordinateMap Map=new(new(),new(),1,1);
        internal bool Down,ThrowWrite;internal int WritesThisTick,Captures,Releases;
        internal PetSnapshot Core;internal DirectInteractionSnapshot Direct;internal PlatformPose? LastPose;internal Exception? Fault;
        private TimeSpan elapsed;private EventHandler? tick;
        internal Harness(Func<IReadOnlyList<DesktopMonitor>>? fallback=null,bool realPresenter=false,bool refuseLegacyArea=false,bool platformEnabled=true,bool walking=false,bool perchEnabled=false)
        {
            if(realPresenter)Presenter=new DororongPresenter{Width=144,Height=144};
            Platforms=new PetPlatformRuntime(new(Native),_=>Map,()=> Presenter is null ? (new(20,100,100,10),new(10,10,100,90)) : Presenter.MeasurePlatformGeometry(),
                (pose,sole)=>{LastPose=pose;Presenter?.ApplyPlatformPose(pose,sole);},fallback,
                perchEnabled ? facing => Presenter?.MeasureEdgePerchContact(facing) ?? new PerchContact(44,74,94,48) : null,
                perchEnabled ? (phase,facing,delta,immediate)=>Presenter?.ApplyEdgePerch(phase,facing,delta,immediate) : null);
            var host=new PetLoopHost(()=>refuseLegacyArea?throw new InvalidOperationException("legacy primary area used"):new(0,0,800,600),()=>Presenter is null?new(120,100):new(144,144),()=>new(4,4),()=>Pointer,()=>Down,()=>Position,
                p=>{if(ThrowWrite)throw new InvalidOperationException("host failure");Position=p;WritesThisTick++;},
                (core,direct)=>{Core=core;Direct=direct;if(Presenter is not null){Presenter.Render(core,direct);Presenter.Measure(new(144,144));Presenter.Arrange(new(0,0,144,144));Presenter.UpdateLayout();}},()=>{Captures++;return true;},()=>{Releases++;Loop!.NotifyDirectInteractionCanceled();}){Platforms=platformEnabled?Platforms:null};
            Loop=new(new(()=>elapsed,()=>{},()=>{}),new(h=>tick+=h,h=>tick-=h,()=>{},()=>{}),host,
                _=>new(BehaviorTuning.Default with {IdleMin=walking?TimeSpan.FromMilliseconds(100):TimeSpan.FromMinutes(10),IdleMax=walking?TimeSpan.FromMilliseconds(100):TimeSpan.FromMinutes(10),IdleToWalkProbability=1},new SeededRandomSource(1),new(100,100)));
            Loop.Faulted+=(_,e)=>Fault=e;
        }
        internal void Start()=>Loop.Start();
        internal void Tick(int ms=16){WritesThisTick=0;elapsed+=TimeSpan.FromMilliseconds(ms);tick?.Invoke(this,EventArgs.Empty);Assert.Null(ThrowWrite?null:Fault);}
        internal void Press(int target)
        {
            var local=new PointD(60,50);Pointer=new(true,Position+local);Down=true;
            BodyPullCapture? body=target is >=1 and <=5?new((BodyRegion)target,new(23,77),System.Windows.Media.Matrix.Identity,Interaction.BodyPullTests.Source()):null;
            var args=new DirectInteractionPressEventArgs(target==0?DirectInteractionTarget.Body:target==6?DirectInteractionTarget.RightCheek:DirectInteractionTarget.FiveRegionBody,local,new(23,77),-1,body);
            if(target==6)args=new DirectInteractionPressEventArgs(DirectInteractionTarget.RightCheek,local,new(16,58),-1){CheekCapture=new(Controls.CheekProductTests.Read("canonical.png"),System.Windows.Media.Matrix.Identity,FacingDirection.Right)};
            Loop.NotifyDirectInteractionPressed(args);
        }
        internal void PressReal(int target,PointD source)
        {
            var p=Presenter!;var image=(System.Windows.Controls.Image)p.FindName("DororongImage");
            var point=image.TranslatePoint(new(source.X*image.ActualWidth/96,source.Y*image.ActualHeight/96),p);
            var local=new PointD(point.X,point.Y);Pointer=new(true,Position+local);Down=true;
            BodyPullCapture? body=null;CheekPullCapture? cheek=null;
            if(target is >=1 and <=5){Assert.True(p.TryCreateBodyPullCapture(source,out body));Assert.Equal((BodyRegion)target,body!.Region);}
            if(target==6)Assert.True(p.TryCreateCheekPullCapture(source,out cheek));
            Loop.NotifyDirectInteractionPressed(new(target==0?DirectInteractionTarget.Body:target==6?DirectInteractionTarget.RightCheek:DirectInteractionTarget.FiveRegionBody,
                local,source,-1,body){CheekCapture=cheek});
        }
        public void Dispose()=>Loop.Dispose();
    }
}
