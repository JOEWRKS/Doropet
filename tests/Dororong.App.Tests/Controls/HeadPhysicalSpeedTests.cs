using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HeadPhysicalSpeedTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_physical_sample_does_not_poison_the_following_valid_sample(double invalid)
    {
        var c=new DirectInteractionController();c.BeginDistanceBody(new(100,100),new(4,4));
        c.Advance(TimeSpan.Zero,Sample(new(100,20),new(500,400)),true,PetState.Dragged,PetState.Dragged);
        var bad=c.Advance(TimeSpan.FromMilliseconds(16),Sample(new(100,20),new(invalid,400)),true,PetState.Dragged,PetState.Dragged);
        Assert.Equal(0,bad.HeadSwingDegrees);
        var good=c.Advance(TimeSpan.FromMilliseconds(16),Sample(new(100,20),new(500,400)),true,PetState.Dragged,PetState.Dragged);
        Assert.Equal(0,good.HeadSwingDegrees);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void Physical_cursor_speed_not_logical_distance_drives_swing(double dpiScale)
    {
        var c=new DirectInteractionController();c.BeginDistanceBody(new(100,100),new(4,4));
        c.Advance(TimeSpan.Zero,Sample(new(100,20),new(500,400)),true,PetState.Dragged,PetState.Dragged);
        DirectInteractionSnapshot result=default;
        for(var i=1;i<=160;i++)
            result=c.Advance(TimeSpan.FromMilliseconds(16),Sample(new(100+i*8/dpiScale,20),new(500+i*8,400)),true,PetState.Dragged,PetState.Dragged);
        Assert.InRange(result.HeadSwingDegrees,39.8,40.2); // 500 px/s -> 40 degrees, all DPI scales
    }

    [Fact]
    public void Logical_position_jump_with_stationary_physical_mouse_does_not_add_swing()
    {
        var c=new DirectInteractionController();c.BeginDistanceBody(new(100,100),new(4,4));
        c.Advance(TimeSpan.Zero,Sample(new(100,20),new(500,400)),true,PetState.Dragged,PetState.Dragged);
        DirectInteractionSnapshot result=default;
        for(var i=1;i<=160;i++)
            result=c.Advance(TimeSpan.FromMilliseconds(16),Sample(new(100+i*24,20),new(500,400)),true,PetState.Dragged,PetState.Dragged);
        Assert.Equal(0,result.HeadSwingDegrees);
    }

    private static PointerSample Sample(PointD logical,PointD physical) =>
        new(true,logical) { ScreenPixelPosition=physical };
}
