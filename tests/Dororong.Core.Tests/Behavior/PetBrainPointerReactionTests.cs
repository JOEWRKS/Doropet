using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainPointerReactionTests
{
    [Fact]
    public void Slow_new_entry_into_the_near_zone_enters_curious()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

        Assert.Equal(PetState.Curious, actual.State);
    }

    [Fact]
    public void Fast_closing_motion_inside_the_reaction_zone_enters_startled()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));

        var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));

        Assert.Equal(PetState.Startled, actual.State);
        Assert.True(actual.Position.X < 100);
    }

    [Fact]
    public void Fast_motion_away_from_the_pet_does_not_startle()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(360, 150)));

        var actual = brain.Update(PetTestInput.At(0.1, pointer: new PointD(370, 150)));

        Assert.NotEqual(PetState.Startled, actual.State);
    }

    [Fact]
    public void A_cursor_that_remains_near_does_not_retrigger_curious()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(1.0, pointer: new PointD(300, 150)));

        for (var index = 0; index < 30; index++)
        {
            brain.Update(PetTestInput.At(0.1, pointer: new PointD(300, 150)));
        }

        Assert.Equal(PetState.Idle, brain.Current.State);
    }
}
