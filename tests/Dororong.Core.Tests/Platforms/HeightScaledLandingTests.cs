using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.Core.Tests.Platforms;

public class HeightScaledLandingTests
{
    private static readonly FootContact Feet=new(20,60,100,20);
    private static readonly PlatformSurface Floor=new(new(0,0,1),1,PlatformKind.Floor,0,1000,1000,new(0,1000));
    private static PlatformPose Step(PlatformMotion motion,double ms) => motion.Advance(new(TimeSpan.FromMilliseconds(ms),motion.Current.Position,motion.Current.Position,Feet,[Floor],false,true));
    private static PlatformMotion Drop(double height)
    {
        var motion=new PlatformMotion(); motion.Reset(new(100,900-height));
        for(var i=0;i<500;i++)
        {
            var pose=Step(motion,8);
            if(pose.Phase==PlatformPhase.Landing) {Assert.Equal(900,pose.Position.Y,6);return motion;}
            Assert.Equal(0,pose.Squash);
        }
        throw new Exception("No contact reached.");
    }

    [Theory]
    [InlineData(3,.036,2.16)] [InlineData(75,.075,4.5)] [InlineData(150,.15,9)]
    [InlineData(300,.30,18)] [InlineData(600,.30,18)]
    public void Actual_fall_distance_scales_compression_and_real_upward_hop_with_a_cap(double height,double squash,double hop)
    {
        var motion=Drop(height);
        var compressed=Step(motion,80);
        Assert.Equal(squash,compressed.Squash,6);
        Assert.Equal(900,compressed.Position.Y,6);
        var airborne=Step(motion,120);
        Assert.Equal(900-hop,airborne.Position.Y,6);
        Assert.Equal(PlatformPhase.Landing,airborne.Phase);
        Assert.Equal(Floor.Key,airborne.Support);
        var completed=Step(motion,250);
        Assert.Equal(PlatformPhase.Supported,completed.Phase);
        Assert.Equal(900,completed.Position.Y,6);Assert.Equal(0,completed.Squash);
        for(var i=0;i<50;i++)Assert.Equal(completed,Step(motion,16));
    }

    [Fact]
    public void New_capture_forgets_previous_impact_and_drag_travel()
    {
        var motion=Drop(600);Step(motion,200);
        var captured=motion.Advance(new(TimeSpan.FromMilliseconds(16),motion.Current.Position,new(800,100),Feet,[Floor],true,true));
        Assert.Equal(PlatformPhase.Suspended,captured.Phase);Assert.Equal(0,captured.Squash);
        motion.Advance(new(TimeSpan.FromMilliseconds(16),captured.Position,new(100,825),Feet,[Floor],true,true));
        for(var i=0;i<100 && motion.Current.Phase!=PlatformPhase.Landing;i++)Step(motion,8);
        Assert.Equal(.075,Step(motion,80).Squash,6);
        Assert.Equal(895.5,Step(motion,120).Position.Y,6);
    }

    [Fact]
    public void Losing_support_mid_rebound_falls_from_visible_position_without_replaying_old_bounce()
    {
        var motion=Drop(300);Step(motion,200);var before=motion.Current;
        Assert.Equal(882,before.Position.Y,6);
        var lost=motion.Advance(new(TimeSpan.FromMilliseconds(16),before.Position,before.Position,Feet,[],false,true));
        Assert.Equal(PlatformPhase.Falling,lost.Phase);
        Assert.Equal(882.2304,lost.Position.Y,6);Assert.Equal(0,lost.Squash);
    }
}
