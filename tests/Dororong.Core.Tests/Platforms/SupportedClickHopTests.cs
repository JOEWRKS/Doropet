using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public class SupportedClickHopTests
{
    [Fact]
    public void Click_offset_never_accumulates_and_owner_loss_falls_from_visible_position()
    {
        var motion=new PlatformMotion();var pos=new PointD(100,190);
        var window=new PlatformSurface(new(1,1,1),1,PlatformKind.Window,0,800,300,new(0,300));
        var floor=new PlatformSurface(new(0,0,1),1,PlatformKind.Floor,0,800,600,new(0,600));
        PlatformPose Step(double offset, bool owner=true)
        {
            var p=motion.Advance(new(TimeSpan.FromMilliseconds(16),pos,pos,new(40,80,110,20),owner?[window,floor]:[floor],false,true)
                {SupportedOffsetY=offset});pos=p.Position;return p;
        }
        for(var i=0;i<60;i++)
        {
            var p=Step(-12);Assert.Equal(PlatformPhase.Supported,p.Phase);Assert.Equal(178,p.Position.Y,6);
        }
        Assert.Equal(190,Step(0).Position.Y,6);
        Step(-12);
        var falling=Step(-12,false);
        Assert.Equal(PlatformPhase.Falling,falling.Phase);Assert.InRange(falling.Position.Y,178.001,179);
    }
}
