using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainDirectInteractionTests
{
    [Fact]
    public void Confirmed_body_click_outranks_a_simultaneous_fast_approach()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        var pressed = brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(260, 150), primaryDown: true,
            bodyPressPosition: new PointD(260, 150)));

        var released = brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(260, 150), primaryDown: false));

        Assert.True(pressed.IsDirectInteractionPending);
        Assert.Equal(PetState.ClickReaction, released.State);
    }

    [Fact]
    public void Body_press_released_before_the_next_tick_is_still_a_click()
    {
        var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));

        var actual = brain.Update(PetTestInput.At(
            0.033,
            pointer: new PointD(160, 160),
            primaryDown: false,
            bodyPressPosition: new PointD(160, 160)));

        Assert.Equal(PetState.ClickReaction, actual.State);
    }

    [Fact]
    public void Unavailable_pointer_on_button_up_cancels_the_pending_interaction_without_a_click()
    {
        var brain = PetTestInput.CreateSleepBrain();
        brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(160, 150),
            primaryDown: true,
            bodyPressPosition: new PointD(160, 150)));

        var released = brain.Update(PetTestInput.At(
            0.1,
            primaryDown: false));

        Assert.Equal(PetState.Idle, released.State);
        Assert.False(released.IsDirectInteractionPending);
        Assert.Null(released.GrabOffset);

        for (var index = 0; index < 8; index++)
        {
            brain.Update(PetTestInput.At(0.1));
        }

        Assert.Equal(PetState.Sleep, brain.Current.State);
    }

    [Fact]
    public void Body_press_snapshot_exposes_the_original_grab_offset_while_pending()
    {
        var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));

        var actual = brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(160, 160),
            primaryDown: true,
            bodyPressPosition: new PointD(160, 160)));

        Assert.True(actual.IsDirectInteractionPending);
        Assert.Equal(new PointD(60, 60), actual.GrabOffset);
    }

    [Theory]
    [InlineData(164, 160)]
    [InlineData(160, 164)]
    public void Reaching_either_system_threshold_enters_dragged(double pointerX, double pointerY)
    {
        var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));
        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 160), primaryDown: true,
            bodyPressPosition: new PointD(160, 160)));

        var actual = brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(pointerX, pointerY), primaryDown: true));

        Assert.Equal(PetState.Dragged, actual.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Movement_just_below_each_system_threshold_stays_pending_then_clicks(bool horizontal)
    {
        var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));
        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 160), primaryDown: true,
            bodyPressPosition: new PointD(160, 160)));
        var justBelowThreshold = Math.BitDecrement(164d);
        var pointer = horizontal
            ? new PointD(justBelowThreshold, 160)
            : new PointD(160, justBelowThreshold);

        var pending = brain.Update(PetTestInput.At(
            0.1, pointer: pointer, primaryDown: true));
        var released = brain.Update(PetTestInput.At(
            0.1, pointer: pointer, primaryDown: false));

        Assert.Equal(PetState.Idle, pending.State);
        Assert.True(pending.IsDirectInteractionPending);
        Assert.Equal(PetState.ClickReaction, released.State);
    }

    [Fact]
    public void Moving_past_the_system_threshold_enters_dragged_with_the_original_grab_offset()
    {
        var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));
        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 160), primaryDown: true,
            bodyPressPosition: new PointD(160, 160)));

        var actual = brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(400, 300), primaryDown: true));

        Assert.Equal(PetState.Dragged, actual.State);
        Assert.Equal(new PointD(340, 240), actual.Position);
        Assert.Equal(new PointD(60, 60), actual.GrabOffset);
    }

    [Theory]
    [InlineData(-10, 300, 0, 240)]
    [InlineData(900, 300, 680, 240)]
    [InlineData(400, -10, 340, 0)]
    [InlineData(400, 700, 340, 500)]
    public void Drag_release_clamps_the_pet_at_each_work_area_edge(
        double pointerX,
        double pointerY,
        double expectedX,
        double expectedY)
    {
        var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));
        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 160), primaryDown: true,
            bodyPressPosition: new PointD(160, 160)));
        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(pointerX, pointerY), primaryDown: true));

        var actual = brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(pointerX, pointerY), primaryDown: false));

        Assert.Equal(PetState.Idle, actual.State);
        Assert.Equal(new PointD(expectedX, expectedY), actual.Position);
        Assert.False(actual.IsDirectInteractionPending);
        Assert.Null(actual.GrabOffset);
    }
}
