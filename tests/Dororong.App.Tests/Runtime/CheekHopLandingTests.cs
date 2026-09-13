using Dororong.App.Interaction;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed partial class PetLoopDirectInteractionTests
{
    [Theory]
    [InlineData(0,true,0)] [InlineData(680,false,680)]
    [InlineData(100,false,108)] [InlineData(100,true,92)]
    public void Cheek_hop_clamps_at_monitor_edges_without_landing_drift(double x,bool mirror,double expectedX) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(()=>{var brain=CreateIdleBrain();brain.ApplyPlatformPosition(new(x,0));return brain;});
        h.Start();h.PressCapturedCheek(mirror);h.Tick();h.MovePointerBy(new(mirror?100:-100,0));h.Tick();h.Release();
        for(var i=0;i<48;i++){h.Tick();Assert.InRange(h.WindowPosition.Y,0,4.01);}
        Assert.Equal(expectedX,h.WindowPosition.X,6);Assert.Equal(0,h.WindowPosition.Y,6);
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Released_cheek_hops_during_recovery_then_lands_behind_without_return(bool mirror) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(CreateIdleBrain);h.Start();h.PressCapturedCheek(mirror);h.Tick();
        h.MovePointerBy(new(mirror?100:-100,0));h.Tick();var rest=h.WindowPosition;
        h.Release();
        for(var i=0;i<4;i++){h.Tick();Assert.Equal(rest,h.WindowPosition);}
        var minimumY=rest.Y;
        for(var i=0;i<24;i++)
        {
            h.Tick();minimumY=Math.Min(minimumY,h.WindowPosition.Y);
            if(h.Renders[^1].Direct.CheekPull is { } visible)
            {Assert.Equal(0,visible.RecoilOffset);Assert.Equal(0,visible.RecoilLift);}
        }
        Assert.InRange(minimumY,rest.Y-4.01,rest.Y-3.8);
        Assert.Equal(rest.X+(mirror?-8:8),h.WindowPosition.X,6);
        Assert.Equal(rest.Y,h.WindowPosition.Y,6);
        Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);
        var landed=h.WindowPosition;for(var i=0;i<10;i++){h.Tick();Assert.Equal(landed,h.WindowPosition);}
    });
}

public partial class PetLoopPlatformTests
{
    [Fact]
    public void Cancelled_cheek_hop_keeps_reached_x_and_falls_to_support() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(realPresenter:true,perchEnabled:true);h.Start();
        h.Native.Scene=new(2,TimeSpan.Zero,[new(1,new(0,0,800,600))],
            [new(new(2,2,2),new(0,560,800,40),0,true,false,false,false,true,true,true,true)]);
        for(var i=0;i<120;i++)h.Tick();
        var rest=h.Position;h.PressReal(6,new(16,58));h.Tick();
        var outward=h.Direct.CheekPull!.Capture.OutwardUnit;
        h.Pointer=new(true,h.Pointer.Position+new PointD(outward.X*100,outward.Y*100));h.Tick();
        h.Down=false;h.Tick();for(var i=0;i<13;i++)h.Tick();
        var airborne=h.Position;Assert.True(airborne.Y<rest.Y-3.7);
        h.Loop.NotifyDirectInteractionCanceled();
        for(var i=0;i<120;i++){h.Tick();Assert.Equal(airborne.X,h.Position.X,5);}
        Assert.Equal(PlatformPhase.Supported,h.LastPose!.Value.Phase);
        Assert.InRange(h.Position.Y,rest.Y-.02,rest.Y+.02);
        Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
    });
    [Fact]
    public void Cheek_hop_lands_at_displaced_taskbar_position() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(realPresenter:true,perchEnabled:true);h.Start();
        h.Native.Scene=new(2,TimeSpan.Zero,[new(1,new(0,0,800,600))],
            [new(new(2,2,2),new(0,560,800,40),0,true,false,false,false,true,true,true,true)]);
        for(var i=0;i<120;i++)h.Tick();
        var rest=h.Position;h.PressReal(6,new(16,58));h.Tick();
        var outward=h.Direct.CheekPull!.Capture.OutwardUnit;
        h.Pointer=new(true,h.Pointer.Position+new PointD(outward.X*100,outward.Y*100));h.Tick();
        h.Down=false;h.Tick();
        for(var i=0;i<4;i++){h.Tick();Assert.Equal(rest.X,h.Position.X,6);Assert.Equal(rest.Y,h.Position.Y,6);}
        var minimumY=rest.Y;
        for(var i=0;i<24;i++){h.Tick();minimumY=Math.Min(minimumY,h.Position.Y);Assert.Equal(1,h.WritesThisTick);}
        Assert.InRange(minimumY,rest.Y-4.1,rest.Y-3.7);
        Assert.Equal(rest.X-Math.Sign(outward.X)*8,h.Position.X,5);
        Assert.Equal(rest.Y,h.Position.Y,5);
        Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        var landed=h.Position;for(var i=0;i<10;i++){h.Tick();Assert.Equal(landed.X,h.Position.X,5);}
    });
}
