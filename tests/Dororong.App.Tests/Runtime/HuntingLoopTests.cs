using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using System.Windows;
using System.Diagnostics;
using System.IO;

namespace Dororong.App.Tests.Runtime;

public sealed class HuntingLoopTests
{
    [Theory]
    [InlineData(130,FacingDirection.Left)]
    [InlineData(230,FacingDirection.Right)]
    public void Nearby_mouse_wakes_sleeping_pet_and_enters_hunt_without_retreat(double x,FacingDirection facing) => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness(sleepSoon:true);h.Loop.Start();
        for(var i=0;i<12;i++)h.Tick();
        Assert.Equal(PetState.Sleep,h.Snapshot.State);
        h.Pointer=new(true,new(600,181));h.Tick();
        Assert.Equal(PetState.Sleep,h.Snapshot.State);
        h.Pointer=new(true,new(double.NaN,181));h.Tick();
        Assert.Equal(PetState.Sleep,h.Snapshot.State);
        h.Pointer=new(true,new(x,181));h.Tick();
        Assert.Equal(PetState.Idle,h.Snapshot.State);
        Assert.Equal(facing,h.Snapshot.Facing);
        for(var i=0;i<30;i++)
        {
            h.Tick();Assert.Equal(PetState.Idle,h.Snapshot.State);
            Assert.Equal(new PointD(100,100),h.Position);
        }
        var image=(System.Windows.Controls.Image)h.Presenter.FindName("DororongImage");
        Assert.IsNotType<System.Windows.Media.Imaging.BitmapImage>(image.Source);
        h.Pointer=PointerSample.Unavailable;
        // Finish recovery, then remain awake until fresh inactivity elapses.
        for(var i=0;i<15;i++)h.Tick();
        Assert.NotEqual(PetState.Sleep,h.Snapshot.State);
        for(var i=0;i<30;i++)h.Tick();
        Assert.Equal(PetState.Sleep,h.Snapshot.State);
    });

    [Theory]
    [InlineData(360,false)] // Fast approach: old STARTLED zone, outside hunt zone.
    [InlineData(310,false)]
    [InlineData(310,true)] // Slow approach into old CURIOUS zone.
    public void Approaching_mouse_cannot_be_intercepted_by_legacy_reactions(double approachX,bool slow) => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness();h.Loop.Start();
        h.Pointer=new(true,new(slow?approachX+20:600,181));h.Tick();
        h.Pointer=new(true,new(approachX,181));h.Tick();
        Assert.Equal(PetState.Idle,h.Snapshot.State);
        Assert.Equal(new PointD(100,100),h.Position);
        h.Pointer=new(true,new(230,181));h.Tick();
        Assert.Equal(PetState.Idle,h.Snapshot.State);
        for(var i=0;i<25;i++){h.Tick();Assert.Equal(new PointD(100,100),h.Position);}
    });

    [Fact]
    public void Hunt_turns_toward_mouse_but_center_jitter_keeps_last_facing() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness();h.Loop.Start();
        foreach(var (x,facing) in new[]{(130d,FacingDirection.Left),(230d,FacingDirection.Right),
            (171d,FacingDirection.Right),(173d,FacingDirection.Right),(130d,FacingDirection.Left)})
        {
            h.Pointer=new(true,new(x,181));h.Tick();
            Assert.Equal(facing,h.Snapshot.Facing);
            var scale=(System.Windows.Media.ScaleTransform)h.Presenter.FindName("BodyScaleTransform");
            Assert.Equal(facing==FacingDirection.Right?-1d:1d,scale.ScaleX);
            Assert.Equal(new PointD(100,100),h.Position);
        }
    });
    [Fact]
    public void Supported_hunt_keeps_actual_sole_on_taskbar() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness(true);h.Loop.Start();for(var i=0;i<40;i++)h.Tick();
        Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
        var origin=h.Position;
        h.Pointer=new(true,origin+new PointD(52,71));
        var elapsed=Stopwatch.StartNew();
        for(var i=0;i<120;i++)
        {
            h.Pointer=new(true,origin+new PointD(i%20<10?35:110,71));
            h.Tick();
            Assert.Equal(origin.X,h.Position.X,6);
            Assert.Equal(i%20<10?FacingDirection.Left:FacingDirection.Right,h.Snapshot.Facing);
            Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
            Assert.Equal(560,h.Position.Y+h.Presenter.MeasurePlatformContact()!.Value.SoleY,4);
        }
        elapsed.Stop();
        var path=Path.Combine(Controls.EdgePerchPresentationTests.ProjectRoot(),"artifacts/repro/hunt-product-20260911/platform-timing.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,$"120 sustained real WPF render + loop/platform measurement ticks: {elapsed.Elapsed.TotalMilliseconds:F1} ms; mean {elapsed.Elapsed.TotalMilliseconds/120:F2} ms/tick\n");
        h.Loop.NotifySitRequested();for(var i=0;i<10;i++)h.Tick();
        var image=(System.Windows.Controls.Image)h.Presenter.FindName("DororongImage");
        Assert.True(ReferenceEquals(image.Source,LocomotionFrames.Sit(1)) || ReferenceEquals(image.Source,LocomotionFrames.Sit(1,true)));
    });
    [Fact]
    public void Proximity_holds_walking_position_then_resumes_after_exit() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness();h.Loop.Start();for(var i=0;i<22;i++)h.Tick();
        Assert.Equal(PetState.Walk,h.Snapshot.State);
        var held=h.Position;h.Pointer=new(true,held+new PointD(52,71));
        for(var i=0;i<45;i++){h.Tick();Assert.Equal(held,h.Position);}
        h.Pointer=PointerSample.Unavailable;
        var facing=h.Snapshot.Facing;
        for(var i=0;i<35;i++)h.Tick();
        Assert.NotEqual(held.X,h.Position.X);
        Assert.Equal(facing==FacingDirection.Left?-1:1,Math.Sign(h.Position.X-held.X));
    });

    [Fact]
    public void Hunt_does_not_swallow_a_head_press() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness();h.Loop.Start();h.Pointer=new(true,h.Position+new PointD(52,71));
        for(var i=0;i<12;i++)h.Tick();
        h.Down=true;
        h.Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.Body,new(52,71),new(28,47),1));
        h.Tick();Assert.True(h.Snapshot.IsDirectInteractionPending);
        h.Pointer=new(true,h.Pointer.Position+new PointD(50,-50));h.Tick();
        Assert.Equal(PetState.Dragged,h.Snapshot.State);
    });

    private sealed class Harness : IDisposable
    {
        internal readonly DororongPresenter Presenter=new();
        internal readonly PetLoop Loop;
        internal PointD Position=new(100,100);
        internal PetSnapshot Snapshot;
        internal bool Down;
        internal PointerSample Pointer=PointerSample.Unavailable;
        internal PlatformPose? LastPose;
        private TimeSpan _elapsed;
        private EventHandler? _tick;
        internal Harness(bool supported=false,bool sleepSoon=false)
        {
            void Render(PetSnapshot s,DirectInteractionSnapshot d,TimeSpan dt)
            {
                Snapshot=s;Presenter.RenderDesktop(s,d,dt);
                Presenter.Measure(new Size(144,144));Presenter.Arrange(new Rect(0,0,144,144));Presenter.UpdateLayout();
            }
            var host=new PetLoopHost(()=>new(0,0,800,600),()=>new(144,144),()=>new(4,4),()=>Pointer,()=>Down,
                ()=>Position,p=>Position=p,(s,d)=>Render(s,d,TimeSpan.Zero),()=>true,()=>{},Render)
            {UpdateHunting=Presenter.UpdateHunting,HoldLocomotionWalk=Presenter.HoldLocomotionWalk,
                SetLocomotionBlocked=Presenter.SetLocomotionBlocked,SetSittingRequested=Presenter.SetSittingRequested,
                Platforms=supported?new PetPlatformRuntime(new(new TaskbarScene()),_=>new(new(),new(),1,1),
                    Presenter.MeasurePlatformGeometry,(pose,sole)=>{LastPose=pose;Presenter.ApplyPlatformPose(pose,sole);}):null};
            Loop=new(new(()=>_elapsed,()=>{},()=>{}),new(h=>_tick+=h,h=>_tick-=h,()=>{},()=>{}),host,
                _=>new(BehaviorTuning.Default with{IdleMin=TimeSpan.FromSeconds(2),IdleMax=TimeSpan.FromSeconds(2),IdleToWalkProbability=sleepSoon?0:1,
                    SleepDelay=sleepSoon?TimeSpan.FromSeconds(1):BehaviorTuning.Default.SleepDelay},
                    new SeededRandomSource(1),Position));
            Loop.Faulted+=(_,error)=>throw new Exception("Loop fault",error);
        }
        internal void Tick(){_elapsed+=TimeSpan.FromMilliseconds(100);_tick?.Invoke(this,EventArgs.Empty);}
        public void Dispose()=>Loop.Dispose();
    }

    private sealed class TaskbarScene : IDesktopSceneNative
    {
        public DesktopScene? Capture(TimeSpan now)=>new(1,now,[new(1,new(0,0,800,600))],
            [new(new(1,1,1),new(0,560,800,40),0,true,false,false,false,true,true,true,true)]);
    }
}
