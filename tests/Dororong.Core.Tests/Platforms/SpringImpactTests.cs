using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public class SpringImpactTests
{
    [Fact]
    public void Impact_spreads_immediately_recoils_then_holds_without_exceeding_approved_compression()
    {
        var motion = new PlatformMotion();
        var position = new PointD(100,90);
        var floor = new PlatformSurface(new(0,0,1),1,PlatformKind.Floor,0,1000,1000,new(0,1000));
        PlatformPose Step(double ms)
        {
            var pose=motion.Advance(new(TimeSpan.FromMilliseconds(ms),position,position,new(40,80,110,20),[floor],false,true)
                {Monitors=[new(1,new(0,0,1000,1000))]});
            position=pose.Position;return pose;
        }
        for(var i=0;i<150 && motion.Current.Phase!=PlatformPhase.Landing;i++) Step(16);
        Assert.True(motion.Current.IsExtremeLanding);
        var fast=Step(16);
        Assert.InRange(fast.LegSpread,.80,.95);
        Assert.InRange(fast.Squash,.4592,.574);
        var peak=Step(19);
        Assert.Equal(1,peak.LegSpread,6);Assert.Equal(.574,peak.Squash,6);
        var recoil=Step(30);
        Assert.InRange(recoil.LegSpread,.92,.97);
        Assert.True(recoil.Squash<peak.Squash);
        Step(35);
        for(var i=0;i<100;i++)
        {
            Assert.Equal(.574,motion.Current.Squash,6);
            Assert.Equal(1,motion.Current.LegSpread,6);
            Assert.Equal(890,motion.Current.Position.Y,6);
            Step(10);
        }
        var hop=Step(250);
        Assert.InRange(890-hop.Position.Y,31,32.001);
        Assert.Equal(PlatformPhase.Landing,hop.Phase);
    }
}
