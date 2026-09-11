using Dororong.App.Interaction;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Unrelated_mouse_down_during_recovery_cannot_recapture_or_restore_stale_position(int target) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness();h.Start();h.Tick();h.Press(target);h.Tick();
        h.Native.Scene=Scene(windows:false);
        h.Pointer=new(true,h.Pointer.Position+new PointD(180,-40));
        for(var i=0;i<15;i++)h.Tick();
        h.Down=false;h.Tick();var released=h.Position;
        h.Down=true;h.Pointer=new(true,new(799,599));h.Tick();
        Assert.Equal(PlatformPhase.Falling,h.LastPose?.Phase);
        Assert.Equal(released.X,h.Position.X,6);
        Assert.True(h.Position.Y>released.Y);
        Assert.False(h.Direct.RequiresCapture);
    });

    [Fact]
    public void Production_loop_uses_horizontal_walking_on_its_support()
    {
        using var h=new Harness(walking:true); h.Start(); h.Tick(100);
        var start=h.Position; h.Tick(100);
        Assert.Equal(2.1,Math.Abs(h.Position.X-start.X),6);
        Assert.Equal(100,h.Position.Y,6);
        Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
    }

    [Theory]
    [InlineData(0,40,30)] [InlineData(1,23,77)] [InlineData(2,43,83)]
    [InlineData(3,66,81)] [InlineData(4,53,70)] [InlineData(5,70,61)] [InlineData(6,16,58)]
    public void Released_real_sprite_keeps_measurable_feet_and_plays_contact_squash_during_recovery(int target,double x,double y) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter:true); h.Start();
        var sole = h.Presenter!.MeasurePlatformContact()!.Value.SoleY;
        h.Native.Scene = Scene(y:100+sole); h.Tick(80);
        h.PressReal(target,new(x,y)); h.Tick();
        h.Pointer = new(true,h.Pointer.Position + new PointD(180,-30));
        for(var i=0;i<15;i++) h.Tick();
        h.Native.Scene = Scene(windows:false);
        var heldSole = h.Presenter.MeasurePlatformContact()!.Value.SoleY;
        h.Position = new(h.Position.X, 600-heldSole-2);
        h.Down=false; h.Tick(80);
        Assert.NotNull(h.Presenter.MeasurePlatformGeometry());
        var landings=0; PlatformPhase? before=null; var peak=0d;
        for(var i=0;i<100;i++)
        {
            var pose=Assert.IsType<PlatformPose>(h.LastPose);
            var geometry=h.Presenter.MeasurePlatformGeometry()!.Value;
            Assert.True(h.Position.Y+geometry.Contact.SoleY <= 600.01, "Actual sprite must not sink below its surface.");
            if(pose.Phase==PlatformPhase.Landing && before!=PlatformPhase.Landing) landings++;
            if(pose.Squash>.04 && peak<=.04)
            {
                var renderedHeight=geometry.Bounds.Height;
                h.Presenter.ApplyPlatformPose(null);
                var unsquashed=h.Presenter.MeasurePlatformGeometry()!.Value;
                Assert.Equal(unsquashed.Bounds.Height*(1-pose.Squash),renderedHeight,5);
                h.Presenter.ApplyPlatformPose(pose,geometry.Contact.SoleY);
            }
            peak=Math.Max(peak,pose.Squash); before=pose.Phase; h.Tick();
        }
        Assert.Equal(1,landings); Assert.True(peak>.03);
        Assert.Equal(600,h.Position.Y+h.Presenter.MeasurePlatformContact()!.Value.SoleY,5);
    });

    // Catches release recovery retaining window ownership or reintroducing the
    // legacy timed head drop before the surface solver gets control.
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Release_falls_immediately_while_shape_recovers_and_lands_once(int target) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(); h.Start(); h.Tick(); h.Press(target); h.Tick();
        h.Native.Scene = Scene(windows: false);
        h.Pointer = new(true, h.Pointer.Position + new PointD(180, -40));
        for (var i = 0; i < 15; i++) h.Tick();
        var released = h.Position;
        h.Down = false; h.Tick();
        Assert.Null(h.Direct.HeadLanding);
        Assert.Equal(PlatformPhase.Falling, h.LastPose?.Phase);
        Assert.Equal(released.Y + .2304, h.Position.Y, 6);
        Assert.Equal(0, h.LastPose?.Squash);
        var previousPhase = PlatformPhase.Falling; var landings = 0; var peak = 0d;
        for (var i = 0; i < 100; i++)
        {
            var previousY = h.Position.Y; h.Tick();
            Assert.Null(h.Direct.HeadLanding);
            var pose = Assert.IsType<PlatformPose>(h.LastPose);
            if(pose.Phase==PlatformPhase.Falling)
                Assert.True(h.Position.Y >= previousY - .000001, "Airborne recovery must not restore a stale carry position.");
            if (pose.Phase == PlatformPhase.Landing && previousPhase != PlatformPhase.Landing) landings++;
            if (pose.Phase == PlatformPhase.Falling) Assert.Equal(0, pose.Squash);
            peak = Math.Max(peak, pose.Squash); previousPhase = pose.Phase;
        }
        Assert.Equal(1, landings); Assert.True(peak > .04);
        Assert.Equal(500, h.Position.Y, 6);
        Assert.Equal(DirectInteractionTarget.None, h.Direct.Target);
        Assert.Equal(h.Position, h.Core.Position);
    });

    [Theory]
    [InlineData(false, 500)] [InlineData(true, 460)]
    public void Release_lands_on_visible_taskbar_or_screen_edge_when_hidden(bool visible, double y)
    {
        using var h = new Harness(); h.Start(); h.Tick(); h.Press(0); h.Tick();
        h.Pointer = new(true, h.Pointer.Position + new PointD(180, -40)); h.Tick();
        h.Native.Scene = new(2, TimeSpan.Zero, [new(1, new(0,0,800,600))],
            visible ? [new(new(2,1,1), new(0,560,800,40), 0, true,false,false,false,true,true,true,true)] : []);
        h.Down = false;
        for (var i = 0; i < 100; i++) h.Tick();
        Assert.Equal(y, h.Position.Y, 6);
        Assert.Equal(PlatformPhase.Supported, h.LastPose?.Phase);
    }
}
