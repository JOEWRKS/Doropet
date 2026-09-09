using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Theory]
    [InlineData(36,110)] [InlineData(120,113)]
    public void Extended_paw_pixels_are_hittable_and_can_interrupt_the_real_presenter_hold(double x,double y) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter:true);h.Start();h.Native.Scene=Scene(windows:false);h.Position=new(300,0);
        for(var i=0;i<150 && h.LastPose?.Phase!=PlatformPhase.Landing;i++)h.Tick();
        h.Tick(100);h.Tick(200);
        var p=h.Presenter!;var body=(Canvas)p.FindName("BodyGroup");
        var image=body.Children.OfType<AlphaHitTestImage>().Single(i=>i.Visibility==Visibility.Visible);
        Assert.True(image.TryGetOpaqueSourcePoint(new(x,y),out _));
        var local=image.TranslatePoint(new(x,y),p);
        Assert.Same(image,VisualTreeHelper.HitTest(p,local)?.VisualHit);
        // Same input-controller boundary invoked by the bubbled BodyGroup press.
        h.Pointer=new(true,h.Position+new PointD(local.X,local.Y));h.Down=true;
        h.Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.Body,new(local.X,local.Y),new(40,40),-1));
        h.Tick(); Assert.Null(h.LastPose);
        h.Pointer=new(true,h.Pointer.Position+new PointD(30,-100));h.Tick();
        Assert.Null(h.LastPose);Assert.True(h.Captures>0);
        Assert.Equal(Visibility.Visible,((Image)p.FindName("DororongImage")).Visibility);
    });

    [Theory]
    [InlineData(100, true)] [InlineData(100.1, false)]
    public void Threshold_uses_landing_monitor_height_in_logical_coordinates(double startY, bool extreme)
    {
        using var h = new Harness(); h.Start();
        h.Map = new(new(), new(), 2, 2);
        h.Native.Scene = new(2, TimeSpan.Zero,
            [new(1,new(0,0,1600,1200)), new(2,new(2000,0,1600,2000))], []);
        h.Position = new(1100, startY);
        h.Tick(80); // Publish the changed physical scene before crossing either monitor floor.
        for (var i=0;i<200 && h.LastPose?.Phase!=PlatformPhase.Landing;i++) h.Tick();
        Assert.Equal(PlatformPhase.Landing,h.LastPose?.Phase);
        h.Tick(100);
        Assert.Equal(extreme,h.LastPose!.Value.IsExtremeLanding);
        if(extreme) Assert.Equal(900,h.Position.Y,6);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Real_sprite_extreme_landing_keeps_contact_through_hold_and_rebound(bool edge) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, walking: true); h.Start();
        h.Native.Scene = Scene(windows:false); h.Position = new(edge ? 0 : 300, 0);
        for(var i=0;i<150 && h.LastPose?.Phase!=PlatformPhase.Landing;i++) h.Tick();
        Assert.True(h.LastPose!.Value.IsExtremeLanding);
        var transitions=0; var wasLanding=false; var hop=0d;
        for(var i=0;i<120;i++)
        {
            h.Tick(); var pose=h.LastPose!.Value;
            if(pose.Phase==PlatformPhase.Landing && !wasLanding) transitions++;
            wasLanding=pose.Phase==PlatformPhase.Landing;
            var sole=h.Position.Y+h.Presenter!.MeasurePlatformContact()!.Value.SoleY;
            Assert.InRange(sole,567.99,600.00001);
            if(pose.LegSpread>=1-1e-12) Assert.Equal(600,sole,5);
            hop=Math.Max(hop,600-sole);
            Assert.Contains(pose.Phase,new[]{PlatformPhase.Landing,PlatformPhase.Supported});
        }
        Assert.Equal(1,transitions); Assert.True(hop>30);
        Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
    });

    [Theory]
    [InlineData(20, true)]
    [InlineData(20.1, false)]
    [InlineData(0, true)]
    public void Eighty_percent_actual_drop_holds_flat_then_hops(double startY, bool extreme)
    {
        using var h = new Harness(); h.Start();
        h.Native.Scene = Scene(windows: false); h.Position = new(100, startY);
        for (var i = 0; i < 150 && h.LastPose?.Phase != PlatformPhase.Landing; i++) h.Tick(16);
        Assert.Equal(PlatformPhase.Landing, h.LastPose?.Phase);
        // 600px monitor, 100px local sole: startY20 means exactly480px actual fall.
        for (var i = 0; i < 10; i++) h.Tick(10);
        if (!extreme)
        {
            Assert.InRange(h.LastPose!.Value.Squash, -.1, .3);
            for (var i = 0; i < 50; i++) h.Tick(10);
            Assert.Equal(PlatformPhase.Supported, h.LastPose?.Phase);
            return;
        }
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(PlatformPhase.Landing, h.LastPose?.Phase);
            Assert.Equal(.574, h.LastPose!.Value.Squash, 6);
            Assert.Equal(500, h.Position.Y, 6);
            h.Tick(10);
        }
        var hop = 0d;
        for (var i = 0; i < 80; i++) { h.Tick(10); hop = Math.Max(hop, 500 - h.Position.Y); }
        Assert.True(hop >= 25, $"Recovery hop was only {hop}px");
        Assert.Equal(PlatformPhase.Supported, h.LastPose?.Phase);
        Assert.Equal(500, h.Position.Y, 6);
    }

    [Fact]
    public void Picking_up_during_extreme_hold_interrupts_landing_immediately()
    {
        using var h = new Harness(); h.Start(); h.Native.Scene = Scene(windows: false); h.Position = new(100, 0);
        for (var i = 0; i < 150 && h.LastPose?.Phase != PlatformPhase.Landing; i++) h.Tick();
        h.Tick(100); h.Tick(200);
        Assert.Equal(.574, h.LastPose!.Value.Squash, 6);
        h.Press(0); h.Tick();
        Assert.Null(h.LastPose);
        h.Pointer = new(true, h.Pointer.Position + new PointD(50, -80)); h.Tick();
        Assert.Null(h.LastPose);
        Assert.True(h.Captures > 0);
    }
}
