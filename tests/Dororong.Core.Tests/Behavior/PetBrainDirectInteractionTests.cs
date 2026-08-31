using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Tests.TestSupport;

namespace Dororong.Core.Tests.Behavior;

public sealed class PetBrainDirectInteractionTests
{
    [Fact]
    public void Local_cheek_interaction_wakes_sleep_and_suppresses_pointer_reaction()
    {
        var brain = PetTestInput.CreateSleepingBrain();

        var woke = brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(460, 150),
            primaryDown: true,
            localInteractionActive: true));
        var approached = brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(160, 150),
            primaryDown: true,
            localInteractionActive: true));

        Assert.Equal(PetState.Idle, woke.State);
        Assert.Equal(PetState.Idle, approached.State);
        Assert.Equal(woke.Phase, approached.Phase, precision: 10);
        Assert.False(approached.IsDirectInteractionPending);
    }

    [Fact]
    public void Local_cheek_release_never_emits_click_or_drag()
    {
        var brain = PetTestInput.CreateReactionBrain();

        var pressed = brain.Update(PetTestInput.At(
            0.1,
            primaryDown: true,
            localInteractionActive: true));
        var released = brain.Update(PetTestInput.At(
            0.1,
            primaryDown: false,
            localInteractionActive: true));
        var settled = brain.Update(PetTestInput.At(0.1));

        Assert.Equal(PetState.Idle, pressed.State);
        Assert.Equal(PetState.Idle, released.State);
        Assert.Equal(PetState.Idle, settled.State);
        Assert.False(settled.IsDirectInteractionPending);
        Assert.Null(settled.GrabOffset);
    }

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
    [InlineData(-10, 300, 0, 240, -100, 400, 0, 340)]
    [InlineData(900, 300, 680, 240, 1000, 400, 680, 340)]
    [InlineData(400, -10, 340, 0, 500, -100, 440, 0)]
    [InlineData(400, 700, 340, 500, 500, 800, 440, 500)]
    public void Held_drag_clamps_threshold_and_subsequent_ticks_at_each_work_area_edge(
        double thresholdPointerX,
        double thresholdPointerY,
        double thresholdExpectedX,
        double thresholdExpectedY,
        double heldPointerX,
        double heldPointerY,
        double heldExpectedX,
        double heldExpectedY)
    {
        var brain = PetTestInput.CreateBrainAt(new PointD(100, 100));
        brain.Update(PetTestInput.At(
            0.1, pointer: new PointD(160, 160), primaryDown: true,
            bodyPressPosition: new PointD(160, 160)));

        var threshold = brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(thresholdPointerX, thresholdPointerY),
            primaryDown: true));
        var held = brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(heldPointerX, heldPointerY),
            primaryDown: true));
        var released = brain.Update(PetTestInput.At(
            0.1,
            pointer: new PointD(heldPointerX, heldPointerY),
            primaryDown: false));

        Assert.Equal(PetState.Dragged, threshold.State);
        Assert.Equal(new PointD(thresholdExpectedX, thresholdExpectedY), threshold.Position);
        Assert.Equal(new PointD(60, 60), threshold.GrabOffset);
        Assert.Equal(PetState.Dragged, held.State);
        Assert.Equal(new PointD(heldExpectedX, heldExpectedY), held.Position);
        Assert.Equal(new PointD(60, 60), held.GrabOffset);
        Assert.Equal(PetState.Idle, released.State);
        Assert.Equal(new PointD(heldExpectedX, heldExpectedY), released.Position);
        Assert.Null(released.GrabOffset);
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

    [Fact]
    public void Pending_press_freezes_walk_until_drag_preserves_the_original_position()
    {
        var brain = CreateWalkingBrain();
        brain.Update(PetTestInput.At(0.1));
        var beforePress = brain.Current;
        var pressPosition = beforePress.Position + new PointD(60, 50);

        var pressed = brain.Update(PetTestInput.At(
            0.2, pointer: pressPosition, primaryDown: true,
            bodyPressPosition: pressPosition));
        var held = brain.Update(PetTestInput.At(
            0.3, pointer: pressPosition, primaryDown: true));
        var dragged = brain.Update(PetTestInput.At(
            0.1, pointer: pressPosition + new PointD(10, 0), primaryDown: true));

        Assert.Equal(PetState.Walk, pressed.State);
        Assert.Equal(beforePress.Position, pressed.Position);
        Assert.Equal(beforePress.Phase, pressed.Phase, precision: 10);
        Assert.Equal(beforePress.Position, held.Position);
        Assert.Equal(beforePress.Phase, held.Phase, precision: 10);
        Assert.Equal(PetState.Dragged, dragged.State);
        Assert.Equal(beforePress.Position + new PointD(10, 0), dragged.Position);
    }

    [Fact]
    public void Pending_press_freezes_startled_until_drag_preserves_the_original_position()
    {
        var brain = PetTestInput.CreateReactionBrain();
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(460, 150)));
        brain.Update(PetTestInput.At(0.1, pointer: new PointD(260, 150)));
        var beforePress = brain.Current;
        var pressPosition = beforePress.Position + new PointD(60, 50);

        var pressed = brain.Update(PetTestInput.At(
            0.1, pointer: pressPosition, primaryDown: true,
            bodyPressPosition: pressPosition));
        var held = brain.Update(PetTestInput.At(
            0.2, pointer: pressPosition, primaryDown: true));
        var dragged = brain.Update(PetTestInput.At(
            0.1, pointer: pressPosition + new PointD(10, 0), primaryDown: true));

        Assert.Equal(PetState.Startled, pressed.State);
        Assert.Equal(beforePress.Position, pressed.Position);
        Assert.Equal(beforePress.Phase, pressed.Phase, precision: 10);
        Assert.Equal(beforePress.Position, held.Position);
        Assert.Equal(beforePress.Phase, held.Phase, precision: 10);
        Assert.Equal(PetState.Dragged, dragged.State);
        Assert.Equal(beforePress.Position + new PointD(10, 0), dragged.Position);
    }

    [Fact]
    public void Fast_click_released_in_the_press_tick_advances_click_reaction_time()
    {
        var brain = CreateWalkingBrain();
        brain.Update(PetTestInput.At(0.1));
        var beforePress = brain.Current;
        var pressPosition = beforePress.Position + new PointD(60, 50);

        var actual = brain.Update(PetTestInput.At(
            0.05,
            pointer: pressPosition,
            primaryDown: false,
            bodyPressPosition: pressPosition));

        Assert.Equal(PetState.ClickReaction, actual.State);
        Assert.Equal(beforePress.Position, actual.Position);
        Assert.Equal(0.1, actual.Phase, precision: 10);
    }

    private static PetBrain CreateWalkingBrain() =>
        new(
            BehaviorTuning.Default with
            {
                MaxDelta = TimeSpan.FromSeconds(1),
                IdleMin = TimeSpan.FromSeconds(0.1),
                IdleMax = TimeSpan.FromSeconds(0.1),
                WalkMin = TimeSpan.FromSeconds(10),
                WalkMax = TimeSpan.FromSeconds(10),
                IdleToWalkProbability = 1,
                SleepDelay = TimeSpan.FromMinutes(10)
            },
            new SequenceRandomSource(0, 0),
            new PointD(100, 100));
}
