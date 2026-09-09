using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public class PlatformRebaseTests
{
    [Theory]
    [InlineData(PlatformPhase.Falling)]
    [InlineData(PlatformPhase.Landing)]
    [InlineData(PlatformPhase.Lifting)]
    [InlineData(PlatformPhase.Supported)]
    public void Rebase_preserves_world_trajectory_owner_and_impact_clock(PlatformPhase phase)
    {
        var floor=new PlatformSurface(new(0,0,1),1,PlatformKind.Floor,0,1920,800,new(0,800));
        var bar=new PlatformSurface(new(1,10,1),1,PlatformKind.Taskbar,0,1920,760,new(0,760)){Bottom=800};
        var contact=new FootContact(40,80,110,20);
        var original=new PlatformMotion();var rebased=new PlatformMotion();
        PlatformSurface[] surfaces=[floor];
        var initial=phase is PlatformPhase.Supported or PlatformPhase.Lifting?new PointD(100,690):new PointD(100,100);
        PlatformPose Step(PlatformMotion m,PointD at,FootContact c)=>m.Advance(new(TimeSpan.FromMilliseconds(16),at,at,c,surfaces,false,true));
        var a=Step(original,initial,contact);var b=Step(rebased,initial,contact);
        if(phase==PlatformPhase.Lifting){surfaces=[bar,floor];a=Step(original,a.Position,contact);b=Step(rebased,b.Position,contact);}
        if(phase==PlatformPhase.Landing){for(int i=0;a.Phase!=phase&&i<100;i++){a=Step(original,a.Position,contact);b=Step(rebased,b.Position,contact);}}
        Assert.Equal(phase,a.Phase);
        if(phase!=PlatformPhase.Lifting){for(int i=0;i<3;i++){a=Step(original,a.Position,contact);b=Step(rebased,b.Position,contact);}}
        const double shift=37;
        rebased.RebasePosition(new(0,shift));
        b=rebased.Current;
        var shifted=new FootContact(40,80,73,-17);
        for(var i=0;i<100;i++)
        {
            a=Step(original,a.Position,contact);b=Step(rebased,b.Position,shifted);
            Assert.Equal(a.Phase,b.Phase);Assert.Equal(a.Support,b.Support);
            Assert.Equal(a.Position.X,b.Position.X,8);
            Assert.Equal(a.Position.Y+110,b.Position.Y+73,8);
            Assert.Equal(a.Squash,b.Squash,8);Assert.Equal(a.Sway,b.Sway,8);
        }
    }
}
