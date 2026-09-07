using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainBodyLocalPositionTests
{
    [Fact]
    public void Local_owner_position_is_clamped_and_retained_after_release()
    {
        var brain = new PetBrain(BehaviorTuning.Default, new SeededRandomSource(42), new(100, 100));
        var input = new PetInput(TimeSpan.FromMilliseconds(16), new(0, 0, 800, 600), new(144, 144), PointerSample.Unavailable, true, true, null, new(4, 4), new(1000, -40));
        Assert.Equal(new(656, 0), brain.Update(input).Position);
        Assert.Equal(new(656, 0), brain.Update(input with { LocalInteractionActive = false, LocalInteractionPosition = null, PrimaryButtonDown = false }).Position);
    }
}
