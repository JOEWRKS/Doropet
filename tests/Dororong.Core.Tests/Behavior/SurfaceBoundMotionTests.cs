using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public class SurfaceBoundMotionTests
{
    [Theory]
    [InlineData(.125, 142)] [InlineData(.375, 58)]
    public void Walking_has_only_horizontal_full_speed_not_a_random_vertical_heading(double heading, double x)
    {
        var brain = PetTestInput.CreateBrain(new SequenceRandomSource(0, heading, 0, 0), new(100,100));
        var input = PetTestInput.At(1) with { SurfaceBoundMotion = true };
        brain.Update(input);
        var moved = brain.Update(input);
        Assert.Equal(new PointD(x,100), moved.Position);
    }

    [Fact]
    public void Startled_retreat_does_not_lift_off_the_support()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(.1, pointer: new(460,300)) with {SurfaceBoundMotion=true});
        var startled = brain.Update(PetTestInput.At(.1, pointer: new(260,200)) with {SurfaceBoundMotion=true});
        Assert.Equal(PetState.Startled, startled.State);
        Assert.Equal(100, startled.Position.Y);
        Assert.True(startled.Position.X < 100);
    }
}
