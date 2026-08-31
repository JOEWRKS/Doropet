using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Interaction;

public sealed class DirectInteractionControllerTests
{
    [Fact]
    public void Begin_locks_target_until_release_or_cancel()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.LeftCheek, new PointD(10, 10), -1);
        controller.Begin(DirectInteractionTarget.RightCheek, new PointD(10, 10), 1);

        var held = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(30, 10)), true, PetState.Idle, PetState.Idle);
        var released = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(30, 10)), false, PetState.Idle, PetState.Idle);

        Assert.Equal(DirectInteractionTarget.LeftCheek, held.Target);
        Assert.Equal(DirectInteractionTarget.LeftCheek, released.Target);
        controller.Advance(DirectInteractionController.CheekReleaseDuration, PointerSample.Unavailable, false, PetState.Idle, PetState.Idle);
        controller.Begin(DirectInteractionTarget.RightCheek, new PointD(10, 10), 1);
        Assert.Equal(DirectInteractionTarget.RightCheek, controller.Current.Target);
        controller.Cancel();
        Assert.Equal(DirectInteractionTarget.None, controller.Current.Target);
        controller.Begin(DirectInteractionTarget.Body, new PointD(10, 10), 0);
        Assert.Equal(DirectInteractionTarget.Body, controller.Current.Target);
    }

    [Theory]
    [InlineData(2, -1)]
    [InlineData(3, 1)]
    public void Cheek_pull_uses_locked_screen_outward_sign(int targetValue, double sign)
    {
        var target = (DirectInteractionTarget)targetValue;
        var controller = new DirectInteractionController();
        controller.Begin(target, new PointD(50, 50), sign);

        var snapshot = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(50 + (10 * sign), 50)), true, PetState.Idle, PetState.Idle);

        Assert.Equal(target, snapshot.Target);
        Assert.Equal(DirectInteractionPhase.CheekPull, snapshot.Phase);
        Assert.Equal(0.5, snapshot.Strength, 3);
    }

    [Fact]
    public void Cheek_pull_clamps_at_twenty_dips_and_damps_vertical_motion()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.RightCheek, new PointD(0, 0), 1);

        var snapshot = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(100, 20)), true, PetState.Idle, PetState.Idle);

        Assert.Equal(DirectInteractionPhase.CheekPull, snapshot.Phase);
        Assert.Equal(1, snapshot.Strength, 3);
    }

    [Fact]
    public void Inward_cheek_motion_remains_press_instead_of_crossing_face()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.RightCheek, new PointD(10, 10), 1);

        var snapshot = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(0, 10)), true, PetState.Idle, PetState.Idle);

        Assert.Equal(DirectInteractionPhase.CheekPress, snapshot.Phase);
        Assert.Equal(0, snapshot.Strength);
    }

    [Fact]
    public void Cheek_release_returns_from_current_strength_in_approximately_220ms()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.RightCheek, new PointD(0, 0), 1);
        controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(10, 0)), true, PetState.Idle, PetState.Idle);

        var released = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(10, 0)), false, PetState.Idle, PetState.Idle);
        var halfway = controller.Advance(TimeSpan.FromMilliseconds(110), PointerSample.Unavailable, false, PetState.Idle, PetState.Idle);
        var completed = controller.Advance(TimeSpan.FromMilliseconds(110), PointerSample.Unavailable, false, PetState.Idle, PetState.Idle);

        Assert.Equal(DirectInteractionPhase.CheekRelease, released.Phase);
        Assert.Equal(0.5, released.Strength, 3);
        Assert.InRange(halfway.Strength, 0.24, 0.26);
        Assert.Equal(DirectInteractionPhase.None, completed.Phase);
        Assert.Equal(DirectInteractionTarget.None, completed.Target);
    }

    [Fact]
    public void Body_stays_pending_until_core_reports_dragged()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.Body, new PointD(0, 0), 0);

        var pending = controller.Advance(TimeSpan.FromSeconds(1), new PointerSample(true, new PointD(100, 100)), true, PetState.Idle, PetState.Idle);
        var entered = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(100, 100)), true, PetState.Idle, PetState.Dragged);

        Assert.Equal(DirectInteractionPhase.BodyPending, pending.Phase);
        Assert.Equal(DirectInteractionPhase.BodyDragEntry, entered.Phase);
    }

    [Fact]
    public void Body_drag_exposes_continuous_entry_hold_and_settle_progress()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.Body, new PointD(20, 30), 0);

        var entered = controller.Advance(
            TimeSpan.Zero,
            new PointerSample(true, new PointD(28, 36)),
            true,
            PetState.Idle,
            PetState.Dragged);
        var entryHalfway = controller.Advance(
            TimeSpan.FromMilliseconds(70),
            new PointerSample(true, new PointD(40, 45)),
            true,
            PetState.Dragged,
            PetState.Dragged);
        var held = controller.Advance(
            TimeSpan.FromMilliseconds(70),
            new PointerSample(true, new PointD(50, 55)),
            true,
            PetState.Dragged,
            PetState.Dragged);
        var released = controller.Advance(
            TimeSpan.Zero,
            new PointerSample(true, new PointD(50, 55)),
            false,
            PetState.Dragged,
            PetState.Idle);
        var settleHalfway = controller.Advance(
            TimeSpan.FromMilliseconds(90),
            PointerSample.Unavailable,
            false,
            PetState.Idle,
            PetState.Idle);
        var completed = controller.Advance(
            TimeSpan.FromMilliseconds(90),
            PointerSample.Unavailable,
            false,
            PetState.Idle,
            PetState.Idle);

        Assert.Equal(DirectInteractionPhase.BodyDragEntry, entered.Phase);
        Assert.Equal(0, entered.Strength);
        Assert.Equal(new PointD(28, 36), entered.PointerPosition);
        Assert.Equal(DirectInteractionPhase.BodyDragEntry, entryHalfway.Phase);
        Assert.Equal(0.5, entryHalfway.Strength, 3);
        Assert.Equal(new PointD(40, 45), entryHalfway.PointerPosition);
        Assert.Equal(DirectInteractionPhase.BodyDragHold, held.Phase);
        Assert.Equal(1, held.Strength);
        Assert.Equal(new PointD(50, 55), held.PointerPosition);
        Assert.Equal(DirectInteractionPhase.BodyDragSettle, released.Phase);
        Assert.Equal(0, released.ReleaseProgress);
        Assert.False(released.RequiresCapture);
        Assert.Equal(DirectInteractionPhase.BodyDragSettle, settleHalfway.Phase);
        Assert.Equal(0.5, settleHalfway.ReleaseProgress, 3);
        Assert.Equal(new PointD(50, 55), settleHalfway.PointerPosition);
        Assert.Equal(DirectInteractionSnapshot.None, completed);
    }

    [Fact]
    public void Drag_release_enters_local_settle_without_retaining_capture()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.Body, new PointD(0, 0), 0);
        controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(0, 0)), true, PetState.Idle, PetState.Dragged);

        var settled = controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(0, 0)), false, PetState.Dragged, PetState.Idle);

        Assert.Equal(DirectInteractionPhase.BodyDragSettle, settled.Phase);
        Assert.False(settled.RequiresCapture);
    }

    [Fact]
    public void Cancel_clears_target_capture_and_release_progress()
    {
        var controller = new DirectInteractionController();
        controller.Begin(DirectInteractionTarget.LeftCheek, new PointD(0, 0), -1);
        controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(-10, 0)), true, PetState.Idle, PetState.Idle);
        controller.Advance(TimeSpan.Zero, new PointerSample(true, new PointD(-10, 0)), false, PetState.Idle, PetState.Idle);

        controller.Cancel();

        Assert.Equal(DirectInteractionSnapshot.None, controller.Current);
    }
}
