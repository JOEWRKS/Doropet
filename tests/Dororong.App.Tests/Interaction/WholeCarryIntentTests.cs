using System.Windows.Media;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Interaction;

public sealed class WholeCarryIntentTests
{
    private static readonly RectD Area = new(0, 0, 800, 600);
    private static readonly SizeD Size = new(120, 100);

    [Fact]
    public void Head_intent_is_true_only_after_the_existing_whole_carry_hold()
    {
        var controller = new DirectInteractionController();
        controller.BeginDistanceBody(new(100, 100), new(4, 4));
        Assert.False(controller.IsWholeCarry);

        controller.Advance(TimeSpan.FromMilliseconds(16), new(true, new(200, 100)), true,
            PetState.Idle, PetState.Dragged);

        Assert.Equal(DirectInteractionPhase.BodyDragHold, controller.Current.Phase);
        Assert.True(controller.IsWholeCarry);
    }

    [Fact]
    public void Local_body_pull_does_not_claim_carry_until_session_really_moves_the_window()
    {
        var controller = new DirectInteractionController();
        controller.BeginBodyPull(BodyPullTests.Capture(), new(100, 100), new(123, 177));
        controller.AdvanceBodyPull(TimeSpan.FromMilliseconds(16), new(true, new(124, 177)), true, Area, Size);
        Assert.False(controller.IsWholeCarry);

        for (var i = 0; i < 30; i++)
            controller.AdvanceBodyPull(TimeSpan.FromMilliseconds(16), new(true, new(220, 177)), true, Area, Size);

        Assert.Equal(BodyPullPhase.Carried, controller.Current.BodyPull?.Phase);
        Assert.True(controller.IsWholeCarry);
    }

    [Fact]
    public void Cheek_pull_uses_the_session_carry_latch_not_the_target_name()
    {
        var controller = new DirectInteractionController();
        var capture = new CheekPullCapture(new byte[96 * 96 * 4], Matrix.Identity, FacingDirection.Right);
        controller.BeginCheekCarry(capture, new(100, 100), new(200, 200));
        controller.AdvanceCheekCarry(TimeSpan.FromMilliseconds(16), new(true, new(195, 200)), true, Area, Size);
        Assert.False(controller.IsWholeCarry);

        for (var i = 0; i < 100; i++)
            controller.AdvanceCheekCarry(TimeSpan.FromMilliseconds(16), new(true, new(150, 200)), true, Area, Size);

        Assert.True(controller.IsWholeCarry);
    }
}
