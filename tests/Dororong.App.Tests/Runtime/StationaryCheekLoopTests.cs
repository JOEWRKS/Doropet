using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed partial class PetLoopDirectInteractionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Long_cheek_pull_only_stretches_at_the_original_window_position(bool mirror) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new LoopHarness(CreateIdleBrain);h.Start();h.PressCapturedCheek(mirror);h.Tick();
        h.MovePointerBy(new(mirror?500:-500,-300));
        for(var i=0;i<90;i++)
        {
            h.Tick();Assert.Equal(new PointD(100,100),h.WindowPosition);
            Assert.Equal(h.WindowPosition,h.Renders[^1].Core.Position);
            Assert.False(h.Renders[^1].Direct.IsPerchReady);
        }
        Assert.Equal(20,h.Renders[^1].Direct.CheekPull!.PullDips);
        h.Release();CheekTicks(h,46);
        Assert.Equal(mirror?92:108,h.WindowPosition.X,6);Assert.Equal(100,h.WindowPosition.Y,6);
        Assert.Equal(DirectInteractionSnapshot.None,h.Renders[^1].Direct);
        Assert.Equal(1,h.ReleaseCount);
    });
}

public partial class PetLoopPlatformTests
{
    [Fact]
    public void Long_cheek_pull_at_taskbar_keeps_support_and_never_cues_or_attaches() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(realPresenter:true,perchEnabled:true);h.Start();
        h.Native.Scene=new(2,TimeSpan.Zero,[new(1,new(0,0,800,600))],
            [new(new(2,2,2),new(0,560,800,40),0,true,false,false,false,true,true,true,true)]);
        for(var i=0;i<120;i++)h.Tick();
        var rest=h.Position;h.PressReal(6,new(16,58));h.Tick();
        var outward=h.Direct.CheekPull!.Capture.OutwardUnit;
        h.Pointer=new(true,h.Pointer.Position+new PointD(outward.X*500,1000));
        for(var i=0;i<90;i++)
        {
            h.Tick();Assert.Equal(rest.X,h.Position.X,6);Assert.Equal(rest.Y,h.Position.Y,6);
            Assert.False(h.Direct.IsPerchReady);Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        }
        Assert.Equal(20,h.Direct.CheekPull!.PullDips);
        h.Down=false;
        for(var i=0;i<5;i++)
        {
            h.Tick();
            Assert.Equal(rest.X,h.Position.X,6);Assert.Equal(rest.Y,h.Position.Y,6);
            Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
        }
        for(var i=0;i<24;i++)h.Tick();
        Assert.Equal(DirectInteractionTarget.None,h.Direct.Target);
        Assert.Equal(rest.X-Math.Sign(outward.X)*8,h.Position.X,6);Assert.Equal(rest.Y,h.Position.Y,6);Assert.Equal(EdgePerchPhase.None,h.Platforms.PerchPhase);
    });
}
