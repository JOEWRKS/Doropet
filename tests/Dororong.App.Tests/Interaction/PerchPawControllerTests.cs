using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Interaction;

public sealed class PerchPawControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tap_waves_three_times_without_translation_then_retires(bool right)
    {
        var target=right?DirectInteractionTarget.PerchRightPaw:DirectInteractionTarget.PerchLeftPaw;
        var c = Start(target);
        var released = Tick(c, 40, default, false);
        Assert.False(released.RequiresCapture);
        for (var i = 0; i < 6; i++)
        {
            var frame = Tick(c, i == 0 ? 50 : 100, default, false);
            Assert.Equal(default, frame.PawPull!.Offset);
            Assert.Equal(target == DirectInteractionTarget.PerchRightPaw, frame.PawPull.Right);
            Assert.Equal(i % 2 == 0 ? 1 : -1, Math.Sign(frame.PawPull.Angle));
        }
        Assert.Equal(DirectInteractionSnapshot.None, Tick(c, 50, default, false));
    }

    [Theory]
    [InlineData(FacingDirection.Left, -1)]
    [InlineData(FacingDirection.Right, 1)]
    public void Drag_is_radially_bounded_and_mirrors_with_the_clicked_source(FacingDirection facing, int sign)
    {
        var c = Start(); c.SetPressContext(facing, false);
        var pulled = Tick(c, 16, new(30,40));
        Assert.Equal(10.8*sign,pulled.PawPull!.Offset.X,8);Assert.Equal(14.4,pulled.PawPull.Offset.Y,8);
        var released = Tick(c, 16, new(-100,-100), false);
        Assert.Equal(pulled.PawPull.Offset, released.PawPull!.Offset);
        var recovering = Tick(c,110,default,false);
        Assert.Equal(5.4*sign,recovering.PawPull!.Offset.X,8);Assert.Equal(7.2,recovering.PawPull.Offset.Y,8);
        Assert.Equal(0,recovering.PawPull.Angle);
        Assert.Equal(DirectInteractionSnapshot.None,Tick(c,110,default,false));
    }

    [Fact]
    public void Returning_a_drag_to_origin_does_not_reclassify_it_as_a_tap()
    {
        var c=Start(); Tick(c,16,new(0,10)); Tick(c,16,default);
        Tick(c,16,default,false);
        Assert.Equal(0,Tick(c,50,default,false).PawPull!.Angle);
        Assert.Equal(DirectInteractionSnapshot.None,Tick(c,170,default,false));
    }

    [Fact]
    public void Long_hold_does_not_start_a_wave_and_invalid_samples_do_not_deform()
    {
        var c=Start();
        var held=c.Current;
        c.Advance(TimeSpan.FromMilliseconds(400),PointerSample.Unavailable,true,PetState.Idle,PetState.Idle);
        Assert.Equal(held,c.Current);
        Tick(c,16,new(double.NaN,double.PositiveInfinity)); Assert.Equal(held,c.Current);
        Assert.Equal(DirectInteractionSnapshot.None,Tick(c,16,default,false));
    }

    private static DirectInteractionController Start(DirectInteractionTarget target=DirectInteractionTarget.PerchLeftPaw)
    {
        var controller=new DirectInteractionController();controller.BeginPerchPaw(target,default);return controller;
    }
    private static DirectInteractionSnapshot Tick(DirectInteractionController c,int ms,PointD pointer,bool down=true) =>
        c.Advance(TimeSpan.FromMilliseconds(ms),new(true,pointer),down,PetState.Idle,PetState.Idle);
}
