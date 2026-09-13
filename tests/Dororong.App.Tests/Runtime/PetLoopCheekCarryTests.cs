using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Runtime;

public sealed partial class PetLoopDirectInteractionTests
{
    [Fact]
    public void Cheek_local_small_pull_tracks_stretch_without_moving_the_window() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new LoopHarness(CreateIdleBrain); h.Start(); h.PressCapturedCheek(); h.Tick();
        h.MovePointerBy(new(-12,0)); h.Tick();
        Assert.Equal(12,h.Renders[^1].Direct.CheekPull!.PullDips);
        CheekTicks(h,60);
        Assert.InRange(h.Renders[^1].Direct.CheekPull!.PullDips, 11, 12);
        Assert.Equal(new(100,100),h.WindowPosition);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cheek_local_full_stretch_stays_extended_at_a_stationary_pointer(bool mirror) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new LoopHarness(CreateIdleBrain); h.Start(); h.PressCapturedCheek(mirror); h.Tick();
        h.MovePointerBy(new(mirror ? 50 : -50,0));
        var peak = 0d; var previous=h.WindowPosition;
        for(var i=0;i<60;i++)
        {
            h.Tick(); var next=h.WindowPosition;
            Assert.InRange(Math.Abs(next.X-previous.X),0,12); previous=next;
            var pull=h.Renders[^1].Direct.CheekPull!.PullDips; Assert.InRange(pull,-10,20);peak=Math.Max(peak,pull);
            Assert.Equal(h.WindowPosition,h.Renders[^1].Core.Position);
        }
        Assert.InRange(peak,15,20);
        Assert.Equal(new PointD(100,100),h.WindowPosition);
        Assert.Equal(20,h.Renders[^1].Direct.CheekPull!.PullDips);
        Assert.DoesNotContain(h.Renders,r=>r.Core.State is PetState.Dragged or PetState.ClickReaction);
        Assert.Equal(1,h.CaptureCount);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cheek_local_reversal_updates_signed_stretch_without_vertical_follow(bool mirror) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new LoopHarness(CreateIdleBrain);h.Start();h.PressCapturedCheek(mirror);h.Tick();
        h.MovePointerBy(new(mirror ? 50 : -50,0));CheekTicks(h,40);
        var carried=h.WindowPosition;
        h.MovePointerBy(new(mirror ? -100 : 100,-40));
        var inward=false;
        for(var i=0;i<60;i++){h.Tick();inward |= h.Renders[^1].Direct.CheekPull!.PullDips < -0.1;}
        Assert.True(inward,"Reversal must change the held cheek, not freeze its entry shape.");
        Assert.Equal(carried,h.WindowPosition);
        Assert.Equal(new PointD(100,100),h.WindowPosition);
        Assert.Equal(-10,h.Renders[^1].Direct.CheekPull!.PullDips);
    });

    [Fact]
    public void Cheek_local_release_lands_behind_without_head_fall() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(CreateIdleBrain){NotifyLostCaptureOnRelease=true};h.Start();h.PressCapturedCheek();h.Tick();
        h.MovePointerBy(new(-60,0));CheekTicks(h,15);
        var held=h.WindowPosition;var pull=h.Renders[^1].Direct.CheekPull!.PullDips;
        Assert.Equal(new PointD(100,100),held);
        h.MovePointerTo(new(700,400));h.Release();
        Assert.Equal(held,h.WindowPosition);Assert.Equal(pull,h.Renders[^1].Direct.CheekPull!.PullDips);
        Assert.False(h.Renders[^1].Direct.RequiresCapture);Assert.Null(h.Renders[^1].Direct.HeadLanding);
        for(var i=0;i<4;i++){h.Tick();Assert.Equal(held,h.WindowPosition);}
        for(var i=0;i<24;i++)h.Tick();
        Assert.Equal(held.X+8,h.WindowPosition.X,6);Assert.Equal(held.Y,h.WindowPosition.Y,6);
        Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);Assert.Equal(1,h.ReleaseCount);
    });

    [Fact]
    public void Cheek_local_offscreen_pointer_only_clamps_stretch_not_window_position() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(CreateIdleBrain);h.Start();h.PressCapturedCheek();h.Tick();
        h.MovePointerTo(new(-1000,-1000));CheekTicks(h,90);Assert.Equal(new(100,100),h.WindowPosition);
        Assert.Equal(20,h.Renders[^1].Direct.CheekPull!.PullDips);
        h.MovePointerTo(new(2000,2000));CheekTicks(h,90);Assert.Equal(new(100,100),h.WindowPosition);
        Assert.Equal(-10,h.Renders[^1].Direct.CheekPull!.PullDips);
        Assert.Equal(h.WindowPosition,h.Renders[^1].Core.Position);
        h.Release();CheekTicks(h,14);Assert.Equal(new(100,100),h.WindowPosition);
    });

    [Theory]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("fault")]
    public void Cheek_local_cleanup_releases_once_and_cannot_move_the_window(string action) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(CreateIdleBrain){NotifyLostCaptureOnRelease=true};h.Start();h.PressCapturedCheek();h.Tick();
        h.MovePointerBy(new(-50,0));CheekTicks(h,30);Assert.Equal(new PointD(100,100),h.WindowPosition);var held=h.WindowPosition;
        if(action=="cancel")h.Cancel();else if(action=="dispose")h.Dispose();else h.FaultOnNextTick();
        h.MovePointerTo(new(400,300));CheekTicks(h,5);
        Assert.Equal(held,h.WindowPosition);Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);Assert.Equal(1,h.ReleaseCount);
        if(action=="cancel"){h.PressCapturedCheek();h.Tick();Assert.Equal(0,h.Renders[^1].Direct.CheekPull!.PullDips);Assert.Equal(held,h.WindowPosition);}
    });

    private static void CheekTicks(LoopHarness h,int count){for(var i=0;i<count;i++)h.Tick();}
}
