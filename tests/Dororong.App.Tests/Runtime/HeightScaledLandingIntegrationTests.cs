using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Real_sprite_growth_at_screen_edge_does_not_reset_landing(bool sideEdge) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(realPresenter:true);h.Start();
        var neutral=h.Presenter!.MeasurePlatformGeometry()!.Value;
        h.Native.Scene=Scene(y:550);if(sideEdge)h.Position=new(-neutral.Bounds.X,100);
        for(var i=0;i<100 && h.LastPose?.Phase!=PlatformPhase.Landing;i++)h.Tick(80);
        Assert.Equal(PlatformPhase.Landing,h.LastPose?.Phase);h.Tick(80);
        if(!sideEdge)h.Native.Scene=Scene(y:neutral.Bounds.Height+5);
        h.Tick(120);
        Assert.Equal(PlatformPhase.Landing,h.LastPose?.Phase);
        var completed=false;
        for(var i=0;i<40;i++)
        {
            h.Tick();var phase=h.LastPose!.Value.Phase;
            Assert.Contains(phase,new[]{PlatformPhase.Landing,PlatformPhase.Supported});
            if(completed)Assert.Equal(PlatformPhase.Supported,phase);
            completed|=phase==PlatformPhase.Supported;
        }
        Assert.True(completed);
    });

    [Fact]
    public void Moving_landing_surface_near_screen_top_clips_hop_without_restarting_impact()
    {
        using var h=new Harness();h.Start();h.Native.Scene=Scene(y:550);
        for(var i=0;i<100 && h.LastPose?.Phase!=PlatformPhase.Landing;i++)h.Tick(80);
        Assert.Equal(PlatformPhase.Landing,h.LastPose?.Phase);h.Tick(80);
        h.Native.Scene=Scene(y:100);h.Tick(120);
        Assert.Equal(PlatformPhase.Landing,h.LastPose?.Phase);
        for(var i=0;i<40;i++)
        {
            h.Tick();
            Assert.Contains(h.LastPose!.Value.Phase,new[]{PlatformPhase.Landing,PlatformPhase.Supported});
            Assert.True(h.Position.Y>=-10.000001);
        }
        Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
        Assert.Equal(0,h.Position.Y,6);
    }

    [Theory]
    [InlineData(75,.07,4.3)] [InlineData(300,.28,17)]
    public void Runtime_keeps_one_landing_and_observable_hop_scaled_by_fall_height(double height,double minimumSquash,double minimumHop)
    {
        using var h=new Harness();h.Start();h.Native.Scene=Scene(windows:false);h.Position=new(100,500-height);
        var peak=0d;var hop=0d;var transitions=0;PlatformPhase? before=null;
        for(var i=0;i<120;i++)
        {
            h.Tick(i==0?80:16);
            var pose=Assert.IsType<PlatformPose>(h.LastPose);
            if(pose.Phase==PlatformPhase.Landing)
            {
                if(before!=PlatformPhase.Landing)transitions++;
                peak=Math.Max(peak,pose.Squash);hop=Math.Max(hop,500-h.Position.Y);
            }
            before=pose.Phase;
        }
        Assert.Equal(1,transitions);Assert.InRange(peak,minimumSquash,.300001);Assert.InRange(hop,minimumHop,18.000001);
        Assert.Equal(500,h.Position.Y,6);Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
    }
}
